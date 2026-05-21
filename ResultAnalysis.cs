using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdamS2T2Docs
{
    class ResultAnalysis
    {
        private string wReturn;
        private string isFinal;
        private int seg_id;
        private string endTime;
        private string roleNum; 

        public string[] returnResult(XfyunData deserializeXfyunData)
        {
            //Console.WriteLine(" --type is : "+deserializeXfyunData.cn.st.type);
            isFinal = deserializeXfyunData.cn.st.type;
            seg_id = deserializeXfyunData.seg_id;
            endTime = deserializeXfyunData.cn.st.ed;
            

            foreach (Rt rt in deserializeXfyunData.cn.st.rt)
            {

                foreach (W w in rt.ws)
                {
                    //Console.WriteLine("wb is: " + w.wb);
                    foreach (Cw cw in w.cw)
                    {
                        //Console.WriteLine("w is :" + cw.w + " ----- wp is : " + cw.wp); ;
                        if (cw.wp != "s")
                        {
                            wReturn += cw.w; // wp词标识	n-普通词；s-顺滑词（语气词）；p-标点
                            if (cw.rl != "0")
                            {
                                roleNum = cw.rl; // 分离的角色编号
                            } 
                        }


                    }
                }

            }

            string[] result = { isFinal, wReturn, seg_id.ToString(), endTime, roleNum};// isFinal: the result is final, wReturn: return "w" (words)
            return result;
        }
    }
}
