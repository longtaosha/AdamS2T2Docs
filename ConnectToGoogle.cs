using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Cloud.Logging.Log4Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Net.Sockets;
using System.Net;


namespace AdamS2T2Docs
{
    class ConnectToGoogle
    {// If modifying these scopes, delete your previously saved credentials folder at bin/Debug/token.json         
        static string[] Scopes = { DocsService.Scope.Documents };
        static string ApplicationName = "Adam's S2T2Googldocs";
        private static string _documentId;
        private static string _documentId0;
        private static string _documentId_backup;
        private static bool isNamed = false;
        private static bool isDashed = false;
        private static bool isInitialized = false;
        private static bool isFirstAppended = true;
        private static bool isFinalReplacedAndNameRangeDeleted = true;  
        private string lastLine = "";

        private int i = 0;
        private int iForFileNameSuffix = 0;
        private int lastLength = 0;
        private int modTime = 0; 
        private int docBodyLength = 0;
        private int doc0CurrentLength = 0;
        private int typeEffect = 0;

        private static string nameOfRange = "Mid_Result";
        private static string myNamedRangeID = "";
        private DocsService service;
        private ParagraphElement element = new ParagraphElement();
        private IDictionary<string, NamedRanges> namedRanges;

        private TcpClient clientForST = null;
        private NetworkStream streamForST;
        private string ipAddressToStreamText = "127.0.0.1";
        private int portForStreamText = 4000;

        public bool isRequstDone = false;
        public bool isWordCopyIsDone = false;
        public bool isWordCopyShouldStop = false;
        public string copiedWordFromDoc = null;
        public enum RequstState{
            NothingDoneYet,
            InsertTextAndNamedRangedCreateDone,
            ReplaceNamedRangeContentDone,
            ReplaceNamedRangeContentAndNamedRangeContentDeleteDone
            
        }
        public RequstState requstState; 



        public ConnectToGoogle(string id, int typeEffectNum, string copyID)
        {
            UserCredential credential;
            using (var stream =
            new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = "token.json";
               
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                // Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Google Docs API service.
            service = new DocsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            

            // Define request parameters.
            //documentId = "1PO1rdNYbfMez7_g1Pv8NUwOcDsfVZxIZk6wfGAZ7cnQ";
            _documentId = id;
            _documentId0 = copyID; 
            typeEffect = typeEffectNum;

        }

        /**
          * Returns the text in the given ParagraphElement.
          *
          * @param element a ParagraphElement from a Google Doc
          */
        private static String readParagraphElement(ParagraphElement element)
        {
            TextRun run = element.TextRun;
            if (run == null || run.Content == null)
            {
                // The TextRun can be null if there is an inline object.
                return "";
            }
            return run.Content;
        }


        /**
          * Returns the very last endIndex in the given StructuralElement (Content).
          *
          * @param element a ParagraphElement from a Google Doc
          */

        private static int? caculateEndindex(IList<StructuralElement> elements)
        {

            int? endIndex = 0;
            foreach (StructuralElement element in elements)
            {
                if (element.EndIndex != null)
                {
                    endIndex = element.EndIndex > endIndex ? element.EndIndex : endIndex;
                }
            }
            return endIndex;
        }

        /**
  * Recurses through a list of Structural Elements to read a document's text where text may be in
  * nested elements.
  *
  * @param elements a list of Structural Elements
  */
        private static string readStructuralElements(IList<StructuralElement> elements)
        {
            StringBuilder sb = new StringBuilder();

            foreach (StructuralElement element in elements)
            {
                if (element.Paragraph != null)
                {
                    foreach (ParagraphElement paragraphElement in element.Paragraph.Elements)
                    {
                        sb.Append(readParagraphElement(paragraphElement));
                    }
                }
            }
            return sb.ToString(); 
        }

        private async Task<Document> getDocumentAsync(string argDocumentId)
        {
            DocumentsResource.GetRequest request = await Task.Run(() => service.Documents.Get(argDocumentId));
            return request.Execute();           
        }

        private async Task sendMyBatchRequest(string argDocumentId, List<Request> requests)
        {
            if (requests.Count > 0)
            {
                BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest {  Requests = requests };
                //await Task.Run(() => service.Documents.BatchUpdate(body, argDocumentId).Execute());
                await service.Documents.BatchUpdate(body, argDocumentId).ExecuteAsync();
            } else return;             
        }

        private async Task<bool> deleteMyNamedRange(string id, string name)
        {
            Document document = await getDocumentAsync(id);
            if (document.NamedRanges != null)
            {
                if (document.NamedRanges[name] != null)
                {
                    List<Request> requestsDelNR = new List<Request> { };
                    requestsDelNR.Add(new Request()
                    {
                        DeleteNamedRange = new DeleteNamedRangeRequest()
                        { Name = name }
                    });

                    await sendMyBatchRequest(id, requestsDelNR);
                    Document document1 = await getDocumentAsync(id);
                    if (document1.NamedRanges == null)
                    {
                        return true;
                    }
                    else {
                        if (document1.NamedRanges[name] == null)
                        {
                            return true;
                        }
                        else return false;                    
                    }
                    
                }
                else return true;
            }
            else return true;
        }

        private async Task<NamedRanges> createMyNamedRange (string id, string name, int startIndex, int endIndex)
        {
            await deleteMyNamedRange(id, name);
            Range range = new Range() { StartIndex = startIndex, EndIndex = endIndex};
            List<Request> requests = new List<Request> { }; 

            requests.Add(new Request()
            {
                CreateNamedRange = new CreateNamedRangeRequest()
                {
                    Name = name,
                    Range = range
                }
            });

            await sendMyBatchRequest(id, requests);

            Document document = await getDocumentAsync(id);
            if (document.NamedRanges != null)
            {
                if (document.NamedRanges[nameOfRange] != null)
                {
                    return document.NamedRanges[nameOfRange];
                }
                else return null;
            } 
            else return null;
        }

        public async Task connectBadAsync(int type, string line)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Document document = await getDocumentAsync(_documentId);
            List<Request> requests = new List<Request> { };
            if (type == 0 && line.Length > 0)// final result
            {
                if (document.NamedRanges != null)
                {
                    if (document.NamedRanges[nameOfRange] != null)
                    {                        
                            requests.Add(new Request()
                            {
                                ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                                { Text = line + "==", NamedRangeName = nameOfRange }
                            });                        

                    }
                }
                else
                {
                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = "==no nameragne!!", EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });
                    isNamed = false;
                }
                if (requests.Count > 0)
                {
                    watch.Stop();
                    var elapsedMs = watch.ElapsedMilliseconds;

                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = elapsedMs.ToString(), EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });

                    await sendMyBatchRequest(_documentId, requests);
                }

                if (await deleteMyNamedRange(_documentId, nameOfRange))
                {
                    isNamed = false;
                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = "-Deleted!", EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });
                    await sendMyBatchRequest(_documentId, requests);

                } else
                {
                    isNamed = true;
                }
            }

            if (type == 1 && line.Length > 0) // middle resulte
            {
                if (document.NamedRanges != null && isNamed == true)
                {
                    if (document.NamedRanges[nameOfRange] != null)
                    {
                        requests.Add(new Request()
                        {
                            ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                            { Text = line + "<" + ">", NamedRangeName = nameOfRange }
                        });
                    }
                    else
                    {
                        requests.Add(new Request()
                        {
                            InsertText = new InsertTextRequest()
                            { Text = "-NO-MID_RESULT!!", EndOfSegmentLocation = new EndOfSegmentLocation() }
                        });
                    }
                    await sendMyBatchRequest(_documentId, requests);
                }
                else
                {

                    Document document2 = await getDocumentAsync(_documentId);
                    if (isInitialized == false)
                    {
                        if (await deleteMyNamedRange(_documentId, nameOfRange))
                        {
                            isInitialized = true;
                        }
                    }

                    if (isNamed == false && isDashed == false)
                    {
                        requests.Add(new Request()
                        {
                            InsertText = new InsertTextRequest()
                            { Text = "  --", EndOfSegmentLocation = new EndOfSegmentLocation() }
                        });                       

                        string str = readStructuralElements(document.Body.Content);
                        docBodyLength = str.Length;
                        Range range = new Range() { StartIndex = docBodyLength, EndIndex = docBodyLength + 4 };
                        requests.Add(new Request()
                        {
                            CreateNamedRange = new CreateNamedRangeRequest()
                            {
                                Name = nameOfRange,
                                Range = range
                            }
                        });
                       
                        await sendMyBatchRequest(_documentId, requests);
                        isDashed = true;


                        Document document3 = await getDocumentAsync(_documentId);

                        if (document3.NamedRanges != null)
                        { 
                            if(document3.NamedRanges[nameOfRange] !=null)
                            {
                               
                                requests.Add(new Request()
                                {
                                    InsertText = new InsertTextRequest()
                                    { Text = "-Created!", EndOfSegmentLocation = new EndOfSegmentLocation() }
                                });
                                await sendMyBatchRequest(_documentId, requests);
                                isNamed = true;

                            }
                        }

                       

                    }
                    
                }
            }
        }

        public async Task <bool> connectReplaceTextAsync(int type, string line, string seg_id)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Document document = await getDocumentAsync(_documentId);
            List<Request> requests = new List<Request> { };

            if (type == 0 && line.Length > 0 )// final result
            {
                if (lastLine.Length == 0)
                {
                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = line, EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });
                }
                else
                {
                    requests.Add(new Request()
                    {
                        ReplaceAllText = new ReplaceAllTextRequest()
                        {
                            ContainsText = new SubstringMatchCriteria { MatchCase = true, Text = lastLine },
                            ReplaceText = line
                        }
                    });
                }

                if (requests.Count > 0)
                {
                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                    BatchUpdateDocumentResponse response = await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();
                    File.AppendAllText("text2google.txt", seg_id + ":::" + line + " - " + 
                      JsonConvert.SerializeObject( response.Replies[0] )
                        +"\n");
                }

                lastLine = "";
                isFirstAppended = true;
                return true;
            }

            if (type == 1 && line.Length > 0) // middle resulte
            {
                if (isFirstAppended == false)
                {               
                    requests.Add(new Request()
                    {
                        ReplaceAllText = new ReplaceAllTextRequest()
                        {
                            ContainsText = new SubstringMatchCriteria { MatchCase = true, Text = lastLine },
                            ReplaceText = "--"+line
                        }
                    });
                    if (requests.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                        BatchUpdateDocumentResponse response = await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();
                                                
                        int? i = response.Replies[0].ReplaceAllText == null ? 0 : response.Replies[0].ReplaceAllText.OccurrencesChanged;

                        File.AppendAllText("text2google.txt", seg_id + "::" + lastLine + " => " + line + " - " +
                          //JsonConvert.SerializeObject(response.Replies[0].ReplaceAllText)
                          i.ToString() + "\n");
                        lastLine = "--" + line;
                        return true;
                    }

                } else
                    {
                        requests.Add(new Request()
                        {
                            InsertText = new InsertTextRequest()
                            { Text = "--"+line, EndOfSegmentLocation = new EndOfSegmentLocation() }
                        });

                        if (requests.Count > 0)
                            {
                            BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                             service.Documents.BatchUpdate(body, _documentId).Execute();
                            //BatchUpdateDocumentResponse response = await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();

                            lastLine = "--" + line;
                             //int? i = response.Replies[0].ReplaceAllText == null? 0 :response.Replies[0].ReplaceAllText.OccurrencesChanged; 

                            File.AppendAllText("text2google.txt", seg_id + ":" + line + " - " + "\n");
                        
                             }
                        
                        document = await getDocumentAsync(_documentId);
                    string str = readStructuralElements(document.Body.Content);
                    string str2 = str.Substring(str.Length - line.Length);
                    /*while (!str2.Equals(line))
                    {
                        document = await getDocumentAsync(_documentId);
                        str = readStructuralElements(document.Body.Content);
                        str2 = str.Substring(str.Length - line.Length);
                    }*/
                    isFirstAppended = false;
                        return true;
                }    
                
            }

            return false;
        }



        public async Task<bool> checkUpdate(string line, int iSuffix)
        {
           Document document = await getDocumentAsync(_documentId);
           string body = readStructuralElements(document.Body.Content);
            int startIndex = (body.Length - line.Length - 1 >= 0) ? (body.Length - line.Length - 1) : 1;
           string lastLine = body.Substring(body.Length - line.Length-1, line.Length);
           
            
            if (lastLine.Equals(line))
            {
                File.AppendAllText("logs/checkUpdate" + iSuffix + ".txt", line + "&" + lastLine);
                return true; 
            }else
            {
                File.AppendAllText("logs/checkUpdate" + iSuffix + "NOT.txt", line + "&" + lastLine);
                return false;
            }        
            
        }

        public async Task<bool> checkNamedRangeDeleteUpdateAsync()
        {
            Document document = await getDocumentAsync(_documentId);
            if (document.NamedRanges != null)
            {
                if (document.NamedRanges[nameOfRange] == null)
                {
                    return true;
                }
                else return false;
            }
            else return true;
        }

        public async Task connectOnlyWaitFinalAsync(int type, string line, bool isToNewLine)
        {
            Document document = await getDocumentAsync(_documentId);
            List<Request> requests = new List<Request> { };
            
            if (type == 0 && line.Length > 0 && isToNewLine == false)// final result
            {
                if (document.NamedRanges != null)
                {
                    if (document.NamedRanges[nameOfRange] != null)
                    {
                        isNamed = true;
                        isFinalReplacedAndNameRangeDeleted = false;
                        requests.Add(new Request()
                            {
                                ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                                { Text = line + "\n", NamedRangeName = nameOfRange }
                            });
                        
                        requests.Add(new Request()
                        {
                            DeleteNamedRange = new DeleteNamedRangeRequest()
                            { Name = nameOfRange }
                        });

                    }
                }
                else
                {
                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = "=="+line+"\n", EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });
                    
                    
                }

                if (requests.Count > 0)
                {
                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest
                    {
                        Requests = requests
                         /* Requests = requests,
                        WriteControl = new WriteControl()
                        {
                            TargetRevisionId = document.RevisionId
                        }*/
                    };

                    await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    
                    isFinalReplacedAndNameRangeDeleted = true;
                    isNamed = false;
                    requests.Clear();
                    return;
                }                
            }
           
            if(type == 0 && line.Length > 0 && isToNewLine == true)
            {

                requests.Add(new Request()
                {
                    InsertText = new InsertTextRequest()
                    { Text = "[pgh]" + line + "\n", EndOfSegmentLocation = new EndOfSegmentLocation() }
                });

                if (requests.Count > 0)
                {
                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                    await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    requests.Clear();
                    return;
                }

            }

            if (type == 1 && line.Length > 0 && isFinalReplacedAndNameRangeDeleted == true) // middle resulte
            {               

                if (document.NamedRanges != null && isNamed == true)
                {
                    if (document.NamedRanges[nameOfRange] != null)
                    {                        
                        requests.Add(new Request()
                        {
                            ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                            { Text = line, NamedRangeName = nameOfRange }
                        });
                    }                    
                    if (requests.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    }
                }
                else
                {     
                    if (isNamed == false)
                    {
                        isNamed = true;
                        requests.Add(new Request()
                        {
                            InsertText = new InsertTextRequest()
                            { Text = "    ", EndOfSegmentLocation = new EndOfSegmentLocation() }
                        });

                        string str = readStructuralElements(document.Body.Content);
                        docBodyLength = str.Length;
                        Range range = new Range() { StartIndex = docBodyLength, EndIndex = docBodyLength + 4 };
                        requests.Add(new Request()
                        {
                            CreateNamedRange = new CreateNamedRangeRequest()
                            {
                                Name = nameOfRange,
                                Range = range
                            }
                        });                        
                    }

                    if (requests.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest
                        {
                            Requests = requests,
                            WriteControl = new WriteControl()
                            {
                                TargetRevisionId = document.RevisionId
                            }
                        };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());                        
                    }
                }
            }
        }

        public async Task connectAppendTextSplitWordAsync(string line)
        {
            Document document = await getDocumentAsync(_documentId);
            List<Request> requests = new List<Request> { };

             string[] word = line.Split(' ');

            foreach (string wd in word)
            {
                requests.Add(new Request()
                {
                    InsertText = new InsertTextRequest()
                    { Text = wd+" ", EndOfSegmentLocation = new EndOfSegmentLocation() }
                });

                File.AppendAllText("logs/googlesplitwords.txt", wd+" ");
                if (requests.Count > 0)
                {
                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                    await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();
                }
                requests.Clear();
            }

            
        }

            public async Task connectTimerAsync(int seg_id, bool isnewline, int type, string line)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Document document = await getDocumentAsync(_documentId);
            List<Request> requests = new List<Request> { };

            requstState = RequstState.NothingDoneYet;
            if (isnewline == true)
            {                

                requests.Add(new Request()
                {
                    InsertText = new InsertTextRequest()
                    { Text = "  --", EndOfSegmentLocation = new EndOfSegmentLocation() }
                });

                string str = readStructuralElements(document.Body.Content);
                docBodyLength = str.Length;
                Range range = new Range() { StartIndex = docBodyLength, EndIndex = docBodyLength + 4 };
                requests.Add(new Request()
                {
                    CreateNamedRange = new CreateNamedRangeRequest()
                    {
                        Name = nameOfRange,
                        Range = range
                    }
                });

                if (requests.Count > 0)
                {
                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                    BatchUpdateDocumentResponse response = await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();
                    if (response.Replies[1].CreateNamedRange != null)
                    {
                        isNamed = true;
                        requstState = RequstState.InsertTextAndNamedRangedCreateDone;
                    }
                    File.AppendAllText("logs/connectGoogle.txt", "seg_id: "+seg_id+" : "+line+"\n");
                }
            }  else 
            {
                switch (type)
                {
                    case 1:
                        if (line.Length > 0) 
                        {
                            if (document.NamedRanges != null && isNamed == true)
                            {
                                if (document.NamedRanges[nameOfRange] != null)
                                {
                                    requests.Add(new Request()
                                    {
                                        ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                                        { Text = line, NamedRangeName = nameOfRange }
                                    });
                                }
                            }
                            if (requests.Count > 0)
                            {
                                isRequstDone = false;
                                BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                                await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();
                                isRequstDone = true;
                                requstState = RequstState.ReplaceNamedRangeContentDone;
                                File.AppendAllText("logs/connectGoogle.txt", "seg_id: " + seg_id + " : " + line + "\n");
                            }
                        }    
                        
                        break;
                    case 0:
                        if (document.NamedRanges != null)
                        {
                            if (document.NamedRanges[nameOfRange] != null)
                            {
                                requests.Add(new Request()
                                {
                                    ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                                    { Text = line + "==", NamedRangeName = nameOfRange }
                                });

                            }
                        }

                        if (document.NamedRanges != null)
                        {
                            if (document.NamedRanges[nameOfRange] != null)
                            {
                                List<Request> requestsDelNR = new List<Request> { };
                                requestsDelNR.Add(new Request()
                                {
                                    DeleteNamedRange = new DeleteNamedRangeRequest()
                                    { Name = nameOfRange }
                                });
                            }
                        }

                        if (requests.Count > 0)
                        {
                            isRequstDone = false;
                            BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                            await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();
                            isRequstDone = true;
                            requstState = RequstState.ReplaceNamedRangeContentAndNamedRangeContentDeleteDone;
                            File.AppendAllText("logs/connectGoogle.txt", "seg_id: " + seg_id + " : " + line + "\n");

                        }                       
                        break;
                }
            }
        }

        public async Task connectOK2Async(int type, string line)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Document document = await getDocumentAsync(_documentId);
            List<Request> requests = new List<Request> { };
            if (type == 0 && line.Length > 0 && isNamed == true)// final result
            {                              
                if (document.NamedRanges != null)
                {
                    if (document.NamedRanges[nameOfRange] != null)
                    {
                        if (modTime > 0)
                        {
                            requests.Add(new Request()
                            {
                                ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                                { Text = line + "==", NamedRangeName = nameOfRange }
                            });
                            modTime = 0;
                        }
                        else
                        {
                            requests.Add(new Request()
                            {
                                InsertText = new InsertTextRequest()
                                { Text = line + "≈≈", EndOfSegmentLocation = new EndOfSegmentLocation() }
                            });
                        }
                    }
                }
                else
                {
                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = "==no nameragne!!", EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });
                    isNamed = false;
                }
                if (requests.Count > 0)
                {
                    watch.Stop();
                    /*var elapsedMs = watch.ElapsedMilliseconds;

                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = elapsedMs.ToString(), EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });*/

                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest
                    {
                        Requests = requests
                    };
                    await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();
                }

                Document document2 = await getDocumentAsync(_documentId);

                isNamed = false;
                if (document2.NamedRanges != null)
                {
                    if (document2.NamedRanges[nameOfRange] != null)
                    {
                        List<Request> requestsDelNR = new List<Request> { };
                        requestsDelNR.Add(new Request()
                        {
                            DeleteNamedRange = new DeleteNamedRangeRequest()
                            { Name = nameOfRange }
                        });

                        if (requestsDelNR.Count > 0)
                        {
                            BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requestsDelNR };
                            await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();
                        }                       
                    }
                }

                document2 = await getDocumentAsync(_documentId);
                /*while (document2.NamedRanges !=null)
                {                    
                    if (document2.NamedRanges[nameOfRange] != null) isNamed = true; ;
                    while (isNamed == true)
                    {                                               
                        document2 = await getDocumentAsync(_documentId);
                        if (document2.NamedRanges == null)
                        {
                            isNamed = false;
                            break;
                        } else if(document2.NamedRanges[nameOfRange] != null)
                            {
                                isNamed = false;
                                break;
                            }
                        }
                    
                }
*/
                bool Checked = false;
                string[] str = new string [10000] ;
                int i = 0;
                str[0] = "\n"+line+"\n";
                while (Checked == false)
                {
                    str[i+1] = i.ToString()+"-";
                    if (document2.NamedRanges == null)
                    { 
                        Checked = true; 
                    } else if (document2.NamedRanges[nameOfRange] == null) Checked = true;
                    document2 = await getDocumentAsync(_documentId);
                    File.AppendAllText("whileloop.txt", str[i]);
                    i++;
                }

            }

            if (type == 1 && line.Length > 0) // middle resulte
            {

                if (document.NamedRanges != null && isNamed == true)
                {
                    if (document.NamedRanges[nameOfRange] != null)
                    {
                        requests.Add(new Request()
                        {
                            ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                            { Text = line + "<" + ">", NamedRangeName = nameOfRange }
                        });
                    }
                    else
                    {
                        requests.Add(new Request()
                        {
                            InsertText = new InsertTextRequest()
                            { Text = "-NO-MID_RESULT!!", EndOfSegmentLocation = new EndOfSegmentLocation() }
                        });
                    }
                    if (requests.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                        await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();
                    }
                }
                else
                {
                    modTime++;
                    if (isNamed == false)
                    {
                        requests.Add(new Request()
                        {
                            InsertText = new InsertTextRequest()
                            { Text = "  --", EndOfSegmentLocation = new EndOfSegmentLocation() }
                        });

                        string str = readStructuralElements(document.Body.Content);
                        docBodyLength = str.Length;
                        Range range = new Range() { StartIndex = docBodyLength, EndIndex = docBodyLength + 4 };
                        requests.Add(new Request()
                        {
                            CreateNamedRange = new CreateNamedRangeRequest()
                            {
                                Name = nameOfRange,
                                Range = range
                            }
                        });
                        isNamed = true;
                    }

                    if (requests.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                        await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();       
                    }

                    document = await getDocumentAsync(_documentId);
                    while (document.NamedRanges == null)
                    {                       
                        isNamed = false;
                        while (isNamed == false)
                        {                            
                            document = await getDocumentAsync(_documentId);
                            if (document.NamedRanges != null)
                            {
                                if (document.NamedRanges[nameOfRange] != null)
                                {
                                    isNamed = true;
                                    break;
                                }
                            }
                        }
                        
                    }


                }
            }
        }

        public async Task connectOKAsync(int type, string line)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Document document = await getDocumentAsync(_documentId);
            List<Request> requests = new List<Request> { };
            if (type == 0 && line.Length > 0)// final result
            {                
                if (document.NamedRanges != null)
                {
                    if (document.NamedRanges[nameOfRange] != null)
                    {
                        if (modTime > 0)
                        {
                            requests.Add(new Request()
                            {
                                ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                                { Text = line + "==", NamedRangeName = nameOfRange }
                            });
                            modTime = 0;
                        }
                        else {
                            requests.Add(new Request()
                            {
                                InsertText = new InsertTextRequest()
                                { Text = line +"≈#≈", EndOfSegmentLocation = new EndOfSegmentLocation() }
                            });                            
                        }
                    }
                }
                else {
                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = "==no nameragne!!", EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });
                    isNamed = false;
                }
                if (requests.Count > 0)
                {
                    watch.Stop();
                    var elapsedMs = watch.ElapsedMilliseconds;

                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = elapsedMs.ToString() , EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });

                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest  { 
                        Requests = requests
                        //WriteControl = new WriteControl() { TargetRevisionId = document.RevisionId }
                        //WriteControl = new WriteControl() { RequiredRevisionId = document.RevisionId }
                    };
                    
                    await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                }

                Document document2 = await getDocumentAsync(_documentId);

                if (document2.NamedRanges != null)
                {
                    if (document2.NamedRanges[nameOfRange] != null)
                    {
                        List<Request> requestsDelNR = new List<Request> { };
                        requestsDelNR.Add(new Request()
                        {
                            DeleteNamedRange = new DeleteNamedRangeRequest()
                            { Name = nameOfRange }
                        });

                        if (requestsDelNR.Count > 0)
                        {
                            BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requestsDelNR };
                            await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                        }
                        isNamed = false;
                    }
                }     
            }

            if (type == 1 && line.Length > 0) // middle resulte
            {
                //int? endIndex = caculateEndindex(document.Body.Content) == null ? 0 : caculateEndindex(document.Body.Content);
               
                /* if (document.NamedRanges != null)
                 {    
                     foreach (KeyValuePair<string, NamedRanges> kvp in namedRanges)
                     {
                         //line += "key is:" + kvp.Key + kvp.Value;
                         //line += "key is:" + tem;
                     }
                 }*/

                if (document.NamedRanges != null && isNamed==true)             
                {                    
                    if (document.NamedRanges[nameOfRange] != null)                   
                    {
                        //var str2 = JsonConvert.SerializeObject( document.NamedRanges[nameOfRange]);
                        string str3 = "*";
                        foreach (NamedRange obj in document.NamedRanges[nameOfRange].NamedRangesValue)
                        {
                            str3 = obj.Name + obj.NamedRangeId+" & " + JsonConvert.SerializeObject(obj.Ranges) + " ";
                        }

                        /*requests.Add(new Request()
                        {
                            InsertText = new InsertTextRequest()
                            { Text = "<"+ str3+">", EndOfSegmentLocation = new EndOfSegmentLocation() }
                        });

                        if (requests.Count > 0)
                        {
                            BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests  };
                            await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                        }*/

                        str3 = "";
                        requests.Add(new Request()
                        {
                            ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                            { Text = line+"<"+str3+">", NamedRangeName = nameOfRange }
                        });         
                        
                    } else{
                        requests.Add(new Request()
                          {
                            InsertText = new InsertTextRequest()
                            { Text = "-NO-MID_RESULT!!", EndOfSegmentLocation = new EndOfSegmentLocation() }
                          });
                       }
                    if (requests.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    }
                }
                else{

                    Document document2 = await getDocumentAsync(_documentId);                                      
                    if (document2.NamedRanges != null &&  false)
                    {
                        if (document2.NamedRanges[nameOfRange] != null)
                        {
                            List<Request> requestsDelNR = new List<Request> { };
                            requestsDelNR.Add(new Request()
                            {
                                DeleteNamedRange = new DeleteNamedRangeRequest()
                                { Name = nameOfRange }
                            });
                            if (requestsDelNR.Count > 0)
                            {
                                BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requestsDelNR };
                                await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                            }
                            isInitialized = true;
                        }

                        string str4 = "";
                        foreach (KeyValuePair<string, NamedRanges> kvp in document2.NamedRanges)
                        {
                            str4 += "key is:" + kvp.Key+"~";
                            if (kvp.Key == nameOfRange)
                            {
                                foreach (NamedRange obj in kvp.Value.NamedRangesValue)
                                {
                                    str4 = obj.NamedRangeId + " & "+ JsonConvert.SerializeObject( obj.Ranges) + " ";                                    
                                }                               
                            } 
                        }
                    }

                    modTime++;
                    if (isNamed == false)
                    {
                        requests.Add(new Request()
                        {
                            InsertText = new InsertTextRequest()
                            { Text ="  --", EndOfSegmentLocation = new EndOfSegmentLocation() }
                        });                        
                        
                        string str = readStructuralElements(document.Body.Content);
                        docBodyLength = str.Length;
                        Range range = new Range() { StartIndex = docBodyLength, EndIndex = docBodyLength+4};
                        requests.Add(new Request()
                        {
                            CreateNamedRange = new CreateNamedRangeRequest()
                            {
                                Name = nameOfRange,
                                Range = range
                            }
                        });
                        isNamed = true;
                    }

                    if (requests.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest  { Requests = requests  };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());                        
                      /*  BatchUpdateDocumentResponse response = new BatchUpdateDocumentResponse
                        {
                            DocumentId = _documentId,
                            Replies = new List<Response> { }
                        };*/                        
                    }
                }         
            }
        }



        public void connect(int type, string line)
        {
                       
            DocumentsResource.GetRequest request = service.Documents.Get(_documentId);         
         
            List<Request> requests = new List<Request> { };
            if (type == 0)// final result
            {

                requests.Add(new Request()
                {
                    InsertText = new InsertTextRequest()
                    { Text = line, EndOfSegmentLocation = new EndOfSegmentLocation() }
                });


                if (requests.Count > 0)
                {
                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                    if (line.Length != 0) service.Documents.BatchUpdate(body, _documentId).Execute(); // should be async
                }
            }

            if (type == 1) // middle resulte
            {
                Document document = request.Execute();
                int docBodyLength = document.Body.Content.ToString().Length;
                Range range = new Range() { StartIndex=docBodyLength, EndIndex=docBodyLength+line.Length};
                requests.Add(new Request()
                {
                    InsertText = new InsertTextRequest()
                    { Text = line, EndOfSegmentLocation = new EndOfSegmentLocation() }
                });

                requests.Add(new Request()
                {
                    CreateNamedRange = new CreateNamedRangeRequest(){Name = "Mid-Result", Range = range}
                });

                if (requests.Count > 0)
                {
                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                    //if (line.Length != 0) service.Documents.BatchUpdate(body, documentId).Execute();
                }
            }
        }

        public void replaceNamedRange(DocsService service, String documentId, String rangeName, String newText)
        {

            // Fetch the document to determine the current indexes of the named ranges.
            Document document = service.Documents.Get(documentId).Execute();
            // Find the matching named ranges.
            
            
        }

        public async Task connectFinalResultAsync(int type, string line)
        {
            Document document = await getDocumentAsync(_documentId);
            List<Request> requests = new List<Request> { };

            if (isNamed == true)
            {
                if (document.NamedRanges != null)
                {
                    if (document.NamedRanges[nameOfRange] != null)
                    {                        
                        requests.Add(new Request()
                        {
                            DeleteNamedRange = new DeleteNamedRangeRequest()
                            { Name = nameOfRange }
                        });

                        if (requests.Count > 0)
                        {
                            BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                            await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                            isNamed = false;
                        }
                        requests.Clear();
                    }
                }
            }


            if (type == 0 && line.Length > 0)// final result
            {
                requests.Add(new Request()
                {
                    InsertText = new InsertTextRequest()
                    { Text = line, EndOfSegmentLocation = new EndOfSegmentLocation() }
                });

                if (requests.Count > 0)
                {
                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                    //await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    try
                    {
                        var response = await service.Documents.BatchUpdate(body, _documentId).ExecuteAsync();
                        if (response != null)
                        {
                            // Access the HTTP status code
                            File.AppendAllText("logs/googleAppendLogs.txt", DateTime.Now.ToString() + " Repose from DeleteRequest: " + response.ToString() + "\n");
                            // Process the response as needed
                            // ...
                        }
                        else
                        {
                            File.AppendAllText("logs/googleAppendLogs.txt", DateTime.Now.ToString() + " Repose is NULL " + "\n");
                            return;
                        }                       
                        
                    }
                    catch (Exception ex)
                    {
                        // Handle the exception
                        File.AppendAllText("logs/googleAppendLogs.txt", DateTime.Now.ToString() + " Exception caught " + ex.ToString()+"\n");
                    }

                    
                }
                 

            }
            else return;

            
        }

        public async Task<int> wordCopy (int lastLenth) //defalut lastLength was given "0".
        {                    
            Document document = await getDocumentAsync(_documentId);
            List<Request> requests = new List<Request> { };
            Document document0 = await getDocumentAsync(_documentId0);
            string str = readStructuralElements(document0.Body.Content);
            int docLength = str.Length;
            if (docLength <= lastLenth)
            {                
                docLength = 0; 
                return docLength;
            }
            else
            {
                str = str.Substring(lastLenth);

                if (str.Length <= 5)
                {
                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = str, EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });
                    if (requests.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    }
                    requests.Clear();
                }
                else
                {
                    for (int i = 0; i < str.Length; i = i + 5)
                    {
                        if (i + 5 > str.Length)
                        {
                            if (i < str.Length)
                            {
                                requests.Add(new Request()
                                {
                                    InsertText = new InsertTextRequest()
                                    { Text = str.Substring(i), EndOfSegmentLocation = new EndOfSegmentLocation() }
                                });
                                if (requests.Count > 0)
                                {
                                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                                    await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                                }
                                requests.Clear();
                            }
                        }
                        else
                        {
                            requests.Add(new Request()
                            {
                                InsertText = new InsertTextRequest()
                                { Text = str.Substring(i, 5), EndOfSegmentLocation = new EndOfSegmentLocation() }
                            });
                            if (requests.Count > 0)
                            {
                                BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                                await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                            }
                            requests.Clear();
                        }
                        isWordCopyIsDone = false;
                    }
                    isWordCopyIsDone = true; 

                }
                return docLength;
            }        

        }


        public async Task wordCopyAndDelete(bool isToStreamtext) //defalut lastLength was given "0".
        {
            _documentId0 = "1B1jlgYVFtWjMwTOB0Bt7ZJ9Oj_CfpfA-GL2XiG4XvSU";
            Document document = await getDocumentAsync(_documentId);
            List<Request> requests = new List<Request> { };
            List<Request> requests0 = new List<Request> { };
            Document document0 = await getDocumentAsync(_documentId0);
            string str = readStructuralElements(document0.Body.Content);
            string strForSteamText = " "; 
            if (str.Contains("**"))
            {
                copiedWordFromDoc = "Doc is in Editing Mode";
                isWordCopyIsDone = true; 
                return;
            }
            int intitialLength = str.Length;
            if (intitialLength <= 4)
            {
                File.AppendAllText("logs/googleErrors.txt", "Length is: " + str.Length.ToString() + ". Docs is Empty" + "\n");
                //MessageBox.Show("initial length is less than 5");
                copiedWordFromDoc = "Docs is Empty"; 
                return;
            }
            else
            {
                // change extraced words to bold 
                if (intitialLength - 1 > 4)
                {
                    requests0.Add(new Request()
                    {
                        UpdateTextStyle = new UpdateTextStyleRequest()
                        { Range = new Range() { StartIndex = 4, EndIndex = intitialLength - 1 }, TextStyle = new TextStyle() { Bold = true }, Fields = "bold" }
                    });

                    if (requests0.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests0 };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId0).Execute());
                    }
                    requests.Clear();
                }
            }

            str = str.Substring(3); // starting index 3 to the end, for there are two "=" at the beginning. 
            str = str.TrimEnd('\n');
            copiedWordFromDoc = str; 
                 if (str.Length <= 5) // doc's initial length is 4, (two "=", one "retrun")
                {
                    requests.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = str, EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });
                    if (requests.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    }
                    requests.Clear();
                    isWordCopyIsDone = true;
                    if (isToStreamtext)
                    {
                        sendText2Streamtext(str, false);
                    }
                }
                else
                {
                    for (int i = 0; i < str.Length; i = i + 5)
                    {
                    if (i + 5 > str.Length)
                    {
                        if (i < str.Length)
                        {

                            requests.Add(new Request()
                            {
                                InsertText = new InsertTextRequest()
                                { Text = str.Substring(i), EndOfSegmentLocation = new EndOfSegmentLocation() }
                            });
                            if (requests.Count > 0)
                            {
                                BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                                await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                            }
                            requests.Clear();

                            if (isToStreamtext)
                            {
                                sendText2Streamtext(str.Substring(i), false);
                            }

                        }
                    }
                    else //   i+5 <=str.Length
                    {
                        await Task.Delay(typeEffect);
                        if (!isWordCopyShouldStop)
                        {
                            bool isBackspace = false;
                            strForSteamText = str.Substring(i, 5);
                            char randomLetter = ' ';

                            if (i == 20 || (i % 20 == 0 && i > 0))
                            {
                                Random random = new Random();
                                int asciiValue = random.Next(65, 91);  // ASCII values for uppercase letters range from 65 to 90
                                randomLetter = (char)asciiValue;
                                strForSteamText = str.Substring(i, 5) + randomLetter + str.Substring(i, 5).ToUpper();
                                isBackspace = true;
                            }
                            else { isBackspace = false; }


                            requests.Add(new Request()
                            {
                                InsertText = new InsertTextRequest()
                                { Text = (i == 20 || (i % 200 ==0 && i>0)) ? str.Substring(i, 5) + randomLetter +str.Substring(i, 5).ToUpper() : str.Substring(i, 5), EndOfSegmentLocation = new EndOfSegmentLocation() }
                            });
                            if (requests.Count > 0)
                            {
                                BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                                await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                            }
                            requests.Clear();

                            if (i == 20 || (i % 200 ==0 && i > 0)) //trying to make delete effect, diidn't work with sending backspace 
                            {
                               
                                await Task.Delay(typeEffect+500);

                                try
                                {
                                    // Retrieve the latest doc
                                    document = await getDocumentAsync(_documentId);
                                    string latestContent = readStructuralElements(document.Body.Content);

                                    requests.Add(new Request()
                                    {
                                        DeleteContentRange = new DeleteContentRangeRequest()
                                        {
                                            Range = new Range
                                            {
                                                StartIndex = latestContent.Length - 6,
                                                EndIndex = latestContent.Length
                                            }
                                        }
                                    });

                                    if (requests.Count > 0)
                                    {

                                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests };
                                        //await Task.Run(() =>service.Documents.BatchUpdate(body, _documentId).Execute());
                                        var response =  service.Documents.BatchUpdate(body, _documentId).Execute();
                                        if (response != null)
                                        {
                                            // Access the HTTP status code
                                            File.AppendAllText("logs/googleDeleteErrors.txt", DateTime.Now.ToString()+" Repose from DeleteRequest: " + response.ToString() + "\n");
                                            // Process the response as needed
                                            // ...
                                        }
                                        else {
                                            File.AppendAllText("logs/googleDeleteErrors.txt", DateTime.Now.ToString()+" Repose is NULL " + "\n");
                                            return;
                                           
                                        }
                                    }
                                    requests.Clear();
                                    await Task.Delay(typeEffect + 1000);
                                }
                                catch (Google.GoogleApiException gae)
                                {
                                    if (gae.HttpStatusCode == System.Net.HttpStatusCode.BadRequest)
                                    {
                                        File.AppendAllText("logs/googleDeleteErrors.txt", DateTime.Now.ToString()+" Bad Request Error: " +gae.ToString() + "\n");
                                    }
                                    else {
                                        
                                        File.AppendAllText("logs/googleDeleteErrors.txt", DateTime.Now.ToString()+"Google API Exception: " + gae.ToString() + "\n");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Handle other exceptions
                                    File.AppendAllText("logs/googleDeleteErrors.txt", "Exception: " + ex.ToString() + "\n");
                                }

                            }

                            requests.Clear();
                            if (isToStreamtext)
                            {
                                sendText2Streamtext(strForSteamText, isBackspace);
                            }
                        }
                        
                    }
                    isWordCopyIsDone = false;                         
                    }
                isWordCopyIsDone = true;
            }


            if (isWordCopyIsDone)
            {
                // delete the words that has been copied
                Range range = new Range() { StartIndex = 4, EndIndex = intitialLength };
                requests0.Add(new Request()
                {
                    DeleteContentRange = new DeleteContentRangeRequest()
                    { Range = range }

                });

                if (requests0.Count > 0)
                {
                    BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests0 };
                    await Task.Run(() => service.Documents.BatchUpdate(body, _documentId0).Execute());
                }
                requests.Clear();
            }

            return; 

        }



        public async Task connectNewAsync (int type, string line)
        {
            
            if (line.Length > 0)
            {
                Document document1 = await getDocumentAsync(_documentId);
                List<Request> requests1 = new List<Request> { };
                if (isNamed == false)
                {
                    requests1.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = "  --", EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });
                  
                    if (requests1.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requests1 };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    }

                    Document documentCreeatNR = await getDocumentAsync(_documentId);
                    List<Request> requestsCreateNR = new List<Request> { };
                    string str = readStructuralElements(documentCreeatNR.Body.Content);
                    docBodyLength = str.Length;
                    Range range = new Range() { StartIndex = docBodyLength-4, EndIndex = docBodyLength};
                    requestsCreateNR.Add(new Request()
                    {
                        CreateNamedRange = new CreateNamedRangeRequest()
                        {
                            Name = nameOfRange,
                            Range = range
                        }
                    });

                    if (requestsCreateNR.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requestsCreateNR };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    }
                    isNamed = true;
                }

                if(isNamed == true && type == 1)
                {
                    List<Request> requestsRepNR = new List<Request> { };
                    requestsRepNR.Add(new Request()
                    {
                        ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                        { Text = line, NamedRangeName = nameOfRange }
                    });

                    if (requestsRepNR.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requestsRepNR };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    }
                }

                if(isNamed == true && type == 0)
                {
                    List<Request> requestsRepNR = new List<Request> { };
                    requestsRepNR.Add(new Request()
                    {
                        ReplaceNamedRangeContent = new ReplaceNamedRangeContentRequest()
                        { Text = "", NamedRangeName = nameOfRange }
                    });

                    if (requestsRepNR.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requestsRepNR };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    }

                    List<Request> requestsDelNR = new List<Request> { };
                    requestsDelNR.Add(new Request()
                    {
                        DeleteNamedRange = new DeleteNamedRangeRequest()
                        { Name = nameOfRange }
                    });

                    requestsDelNR.Add(new Request()
                    {
                        InsertText = new InsertTextRequest()
                        { Text = line, EndOfSegmentLocation = new EndOfSegmentLocation() }
                    });

                    if (requestsDelNR.Count > 0)
                    {
                        BatchUpdateDocumentRequest body = new BatchUpdateDocumentRequest { Requests = requestsDelNR };
                        await Task.Run(() => service.Documents.BatchUpdate(body, _documentId).Execute());
                    }
                    isNamed = false;
                    
                }
            }




        }

        private async void sendText2Streamtext(string texts, bool isBackSpace)
        {
            // Check if the TcpClient instance exists
            if (clientForST == null)
            {
                // initilaize TcpClient instance
                clientForST = new TcpClient(ipAddressToStreamText, portForStreamText);

            }
            try
            {
                // Get the network stream for reading and writing
                streamForST = clientForST.GetStream();
                // Convert the text to send into bytes
                string textToSend = texts;
                byte[] data = Encoding.UTF8.GetBytes(textToSend);

                // Send the text data over the network stream
                streamForST.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                File.AppendAllText("logs/sendText2Streamtexlog.txt", DateTime.Now.ToString() + "Exception: " + ex.ToString() + "\n");
            }

            await Task.Delay(200);
            
            if (isBackSpace)
            {
                try
                {
                    // Get the network stream for reading and writing
                    streamForST = clientForST.GetStream();
                    // Convert the text to send into bytes
                    string textToSend = texts;
                    byte[] data = Encoding.UTF8.GetBytes("\b\b\b\b\b\b");

                    // Send the text data over the network stream
                    streamForST.Write(data, 0, data.Length);
                }
                catch (Exception ex)
                {
                    File.AppendAllText("logs/sendText2Streamtexlog.txt", DateTime.Now.ToString() + "Exception: " + ex.ToString() + "\n");
                }

            }

        }

    }
}
