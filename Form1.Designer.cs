
namespace AdamS2T2Docs
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.startRecordButton = new System.Windows.Forms.Button();
            this.stopRecordButton = new System.Windows.Forms.Button();
            this.audioSourceGroupBox = new System.Windows.Forms.GroupBox();
            this.audioFromSysCheckBox = new System.Windows.Forms.CheckBox();
            this.audioFromMicCheckBox = new System.Windows.Forms.CheckBox();
            this.audioSourceButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.labelTime = new System.Windows.Forms.Label();
            this.realDocsButton = new System.Windows.Forms.Button();
            this.radioButtonJustFinal = new System.Windows.Forms.RadioButton();
            this.radioButtonWordByWord = new System.Windows.Forms.RadioButton();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.languageGroupBox = new System.Windows.Forms.GroupBox();
            this.cnenRadioButton = new System.Windows.Forms.RadioButton();
            this.enRadioButton = new System.Windows.Forms.RadioButton();
            this.buttonWordCopy = new System.Windows.Forms.Button();
            this.buttonStopWordCopy = new System.Windows.Forms.Button();
            this.richTextBoxWordCopy = new System.Windows.Forms.RichTextBox();
            this.buttonToStreamText = new System.Windows.Forms.Button();
            this.buttonStopStreamText = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.buttonCaption = new System.Windows.Forms.Button();
            this.audioSourceGroupBox.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.languageGroupBox.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // richTextBox1
            // 
            this.richTextBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBox1.Font = new System.Drawing.Font("Arial", 14.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.richTextBox1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.richTextBox1.HideSelection = false;
            this.richTextBox1.Location = new System.Drawing.Point(4, 2);
            this.richTextBox1.Margin = new System.Windows.Forms.Padding(2, 33, 2, 16);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.richTextBox1.Size = new System.Drawing.Size(767, 289);
            this.richTextBox1.TabIndex = 0;
            this.richTextBox1.Text = "";
            this.richTextBox1.Click += new System.EventHandler(this.richTextBox1_Click);
            // 
            // startRecordButton
            // 
            this.startRecordButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.startRecordButton.Location = new System.Drawing.Point(62, 449);
            this.startRecordButton.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.startRecordButton.Name = "startRecordButton";
            this.startRecordButton.Size = new System.Drawing.Size(70, 31);
            this.startRecordButton.TabIndex = 3;
            this.startRecordButton.Text = "start";
            this.startRecordButton.UseVisualStyleBackColor = true;
            this.startRecordButton.Click += new System.EventHandler(this.startRecord_Click);
            // 
            // stopRecordButton
            // 
            this.stopRecordButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.stopRecordButton.Location = new System.Drawing.Point(475, 449);
            this.stopRecordButton.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.stopRecordButton.Name = "stopRecordButton";
            this.stopRecordButton.Size = new System.Drawing.Size(72, 31);
            this.stopRecordButton.TabIndex = 4;
            this.stopRecordButton.Text = "stop";
            this.stopRecordButton.UseVisualStyleBackColor = true;
            this.stopRecordButton.Click += new System.EventHandler(this.stopRecord_Click);
            // 
            // audioSourceGroupBox
            // 
            this.audioSourceGroupBox.BackColor = System.Drawing.Color.MistyRose;
            this.audioSourceGroupBox.Controls.Add(this.audioFromSysCheckBox);
            this.audioSourceGroupBox.Controls.Add(this.audioFromMicCheckBox);
            this.audioSourceGroupBox.Location = new System.Drawing.Point(136, 342);
            this.audioSourceGroupBox.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.audioSourceGroupBox.Name = "audioSourceGroupBox";
            this.audioSourceGroupBox.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.audioSourceGroupBox.Size = new System.Drawing.Size(248, 98);
            this.audioSourceGroupBox.TabIndex = 6;
            this.audioSourceGroupBox.TabStop = false;
            this.audioSourceGroupBox.Text = "Audio Source";
            this.audioSourceGroupBox.Visible = false;
            // 
            // audioFromSysCheckBox
            // 
            this.audioFromSysCheckBox.AutoSize = true;
            this.audioFromSysCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.audioFromSysCheckBox.Location = new System.Drawing.Point(16, 67);
            this.audioFromSysCheckBox.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.audioFromSysCheckBox.Name = "audioFromSysCheckBox";
            this.audioFromSysCheckBox.Size = new System.Drawing.Size(163, 24);
            this.audioFromSysCheckBox.TabIndex = 3;
            this.audioFromSysCheckBox.Text = "System Soundcard";
            this.audioFromSysCheckBox.UseVisualStyleBackColor = true;
            this.audioFromSysCheckBox.CheckedChanged += new System.EventHandler(this.audioFromSysCheckBox_CheckedChanged);
            this.audioFromSysCheckBox.Click += new System.EventHandler(this.audioFromSysCheckBox_Click);
            // 
            // audioFromMicCheckBox
            // 
            this.audioFromMicCheckBox.AutoSize = true;
            this.audioFromMicCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.audioFromMicCheckBox.Location = new System.Drawing.Point(16, 30);
            this.audioFromMicCheckBox.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.audioFromMicCheckBox.Name = "audioFromMicCheckBox";
            this.audioFromMicCheckBox.Size = new System.Drawing.Size(111, 24);
            this.audioFromMicCheckBox.TabIndex = 2;
            this.audioFromMicCheckBox.Text = "Microphone";
            this.audioFromMicCheckBox.UseVisualStyleBackColor = true;
            this.audioFromMicCheckBox.CheckedChanged += new System.EventHandler(this.audioFromMicCheckBox_CheckedChanged);
            this.audioFromMicCheckBox.Click += new System.EventHandler(this.audioFromMicCheckBox_Click);
            // 
            // audioSourceButton
            // 
            this.audioSourceButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.audioSourceButton.Location = new System.Drawing.Point(160, 449);
            this.audioSourceButton.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.audioSourceButton.Name = "audioSourceButton";
            this.audioSourceButton.Size = new System.Drawing.Size(103, 31);
            this.audioSourceButton.TabIndex = 7;
            this.audioSourceButton.Text = "Audio Source";
            this.audioSourceButton.UseVisualStyleBackColor = true;
            this.audioSourceButton.Click += new System.EventHandler(this.audioSourceButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(172, 16);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 13);
            this.label1.TabIndex = 8;
            this.label1.Text = "label1";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(460, 16);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(35, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "label2";
            this.label2.Click += new System.EventHandler(this.label2_Click);
            // 
            // timer1
            // 
            this.timer1.Interval = 1000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // labelTime
            // 
            this.labelTime.AutoSize = true;
            this.labelTime.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelTime.Location = new System.Drawing.Point(52, 427);
            this.labelTime.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelTime.Name = "labelTime";
            this.labelTime.Size = new System.Drawing.Size(71, 20);
            this.labelTime.TabIndex = 10;
            this.labelTime.Text = "00:00:00";
            this.labelTime.Click += new System.EventHandler(this.labelTime_Click);
            // 
            // realDocsButton
            // 
            this.realDocsButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.realDocsButton.Location = new System.Drawing.Point(7, 7);
            this.realDocsButton.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.realDocsButton.Name = "realDocsButton";
            this.realDocsButton.Size = new System.Drawing.Size(114, 23);
            this.realDocsButton.TabIndex = 11;
            this.realDocsButton.Text = "To Real Docs";
            this.realDocsButton.UseVisualStyleBackColor = true;
            this.realDocsButton.Click += new System.EventHandler(this.realDocsButton_Click);
            // 
            // radioButtonJustFinal
            // 
            this.radioButtonJustFinal.AutoSize = true;
            this.radioButtonJustFinal.Checked = true;
            this.radioButtonJustFinal.Location = new System.Drawing.Point(3, 15);
            this.radioButtonJustFinal.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.radioButtonJustFinal.Name = "radioButtonJustFinal";
            this.radioButtonJustFinal.Size = new System.Drawing.Size(102, 17);
            this.radioButtonJustFinal.TabIndex = 14;
            this.radioButtonJustFinal.TabStop = true;
            this.radioButtonJustFinal.Text = "Just Final Result";
            this.radioButtonJustFinal.UseVisualStyleBackColor = true;
            this.radioButtonJustFinal.CheckedChanged += new System.EventHandler(this.radioButtonJustFinal_CheckedChanged);
            // 
            // radioButtonWordByWord
            // 
            this.radioButtonWordByWord.AutoSize = true;
            this.radioButtonWordByWord.Location = new System.Drawing.Point(110, 15);
            this.radioButtonWordByWord.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.radioButtonWordByWord.Name = "radioButtonWordByWord";
            this.radioButtonWordByWord.Size = new System.Drawing.Size(95, 17);
            this.radioButtonWordByWord.TabIndex = 15;
            this.radioButtonWordByWord.Text = "Word By Word";
            this.radioButtonWordByWord.UseVisualStyleBackColor = true;
            this.radioButtonWordByWord.CheckedChanged += new System.EventHandler(this.radioButtonWordByWord_CheckedChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioButtonJustFinal);
            this.groupBox1.Controls.Add(this.radioButtonWordByWord);
            this.groupBox1.Location = new System.Drawing.Point(475, 31);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox1.Size = new System.Drawing.Size(243, 35);
            this.groupBox1.TabIndex = 16;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "OUTPUT to Google";
            // 
            // languageGroupBox
            // 
            this.languageGroupBox.Controls.Add(this.cnenRadioButton);
            this.languageGroupBox.Controls.Add(this.enRadioButton);
            this.languageGroupBox.Location = new System.Drawing.Point(160, 31);
            this.languageGroupBox.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.languageGroupBox.Name = "languageGroupBox";
            this.languageGroupBox.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.languageGroupBox.Size = new System.Drawing.Size(243, 35);
            this.languageGroupBox.TabIndex = 17;
            this.languageGroupBox.TabStop = false;
            this.languageGroupBox.Text = "Language";
            this.languageGroupBox.Visible = false;
            // 
            // cnenRadioButton
            // 
            this.cnenRadioButton.AutoSize = true;
            this.cnenRadioButton.Location = new System.Drawing.Point(3, 15);
            this.cnenRadioButton.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.cnenRadioButton.Name = "cnenRadioButton";
            this.cnenRadioButton.Size = new System.Drawing.Size(55, 17);
            this.cnenRadioButton.TabIndex = 14;
            this.cnenRadioButton.Text = "CN&EN";
            this.cnenRadioButton.UseVisualStyleBackColor = true;
            this.cnenRadioButton.CheckedChanged += new System.EventHandler(this.cnenRadioButton_CheckedChanged);
            // 
            // enRadioButton
            // 
            this.enRadioButton.AutoSize = true;
            this.enRadioButton.Checked = true;
            this.enRadioButton.Location = new System.Drawing.Point(110, 15);
            this.enRadioButton.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.enRadioButton.Name = "enRadioButton";
            this.enRadioButton.Size = new System.Drawing.Size(40, 17);
            this.enRadioButton.TabIndex = 15;
            this.enRadioButton.TabStop = true;
            this.enRadioButton.Text = "EN";
            this.enRadioButton.UseVisualStyleBackColor = true;
            this.enRadioButton.CheckedChanged += new System.EventHandler(this.enRadioButton_CheckedChanged);
            // 
            // buttonWordCopy
            // 
            this.buttonWordCopy.Location = new System.Drawing.Point(7, 42);
            this.buttonWordCopy.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonWordCopy.Name = "buttonWordCopy";
            this.buttonWordCopy.Size = new System.Drawing.Size(83, 24);
            this.buttonWordCopy.TabIndex = 18;
            this.buttonWordCopy.Text = "WordCopy";
            this.buttonWordCopy.UseVisualStyleBackColor = true;
            this.buttonWordCopy.Click += new System.EventHandler(this.buttonWordCopy_Click);
            // 
            // buttonStopWordCopy
            // 
            this.buttonStopWordCopy.Enabled = false;
            this.buttonStopWordCopy.Location = new System.Drawing.Point(7, 79);
            this.buttonStopWordCopy.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonStopWordCopy.Name = "buttonStopWordCopy";
            this.buttonStopWordCopy.Size = new System.Drawing.Size(83, 24);
            this.buttonStopWordCopy.TabIndex = 19;
            this.buttonStopWordCopy.Text = "StopCopy";
            this.buttonStopWordCopy.UseVisualStyleBackColor = true;
            this.buttonStopWordCopy.Click += new System.EventHandler(this.buttonStopWordCopy_Click);
            // 
            // richTextBoxWordCopy
            // 
            this.richTextBoxWordCopy.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxWordCopy.Font = new System.Drawing.Font("Arial", 14.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.richTextBoxWordCopy.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.richTextBoxWordCopy.HideSelection = false;
            this.richTextBoxWordCopy.Location = new System.Drawing.Point(4, 4);
            this.richTextBoxWordCopy.Margin = new System.Windows.Forms.Padding(2, 33, 2, 16);
            this.richTextBoxWordCopy.Name = "richTextBoxWordCopy";
            this.richTextBoxWordCopy.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.richTextBoxWordCopy.Size = new System.Drawing.Size(767, 287);
            this.richTextBoxWordCopy.TabIndex = 20;
            this.richTextBoxWordCopy.Text = "";
            // 
            // buttonToStreamText
            // 
            this.buttonToStreamText.Location = new System.Drawing.Point(153, 79);
            this.buttonToStreamText.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonToStreamText.Name = "buttonToStreamText";
            this.buttonToStreamText.Size = new System.Drawing.Size(97, 24);
            this.buttonToStreamText.TabIndex = 21;
            this.buttonToStreamText.Text = "ToStreamText";
            this.buttonToStreamText.UseVisualStyleBackColor = true;
            this.buttonToStreamText.Click += new System.EventHandler(this.buttonToStreamText_Click);
            // 
            // buttonStopStreamText
            // 
            this.buttonStopStreamText.Enabled = false;
            this.buttonStopStreamText.Location = new System.Drawing.Point(259, 79);
            this.buttonStopStreamText.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonStopStreamText.Name = "buttonStopStreamText";
            this.buttonStopStreamText.Size = new System.Drawing.Size(94, 24);
            this.buttonStopStreamText.TabIndex = 22;
            this.buttonStopStreamText.Text = "StopStreamtext";
            this.buttonStopStreamText.UseVisualStyleBackColor = true;
            this.buttonStopStreamText.Click += new System.EventHandler(this.buttonStopStreamText_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(7, 106);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(783, 317);
            this.tabControl1.TabIndex = 23;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.richTextBox1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabPage1.Size = new System.Drawing.Size(775, 291);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Speech2Text";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.richTextBoxWordCopy);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabPage2.Size = new System.Drawing.Size(775, 291);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "wordCOPY";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // buttonCaption
            // 
            this.buttonCaption.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.857143F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonCaption.Location = new System.Drawing.Point(327, 455);
            this.buttonCaption.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonCaption.Name = "buttonCaption";
            this.buttonCaption.Size = new System.Drawing.Size(76, 25);
            this.buttonCaption.TabIndex = 24;
            this.buttonCaption.Text = "Caption";
            this.buttonCaption.UseVisualStyleBackColor = true;
            this.buttonCaption.Click += new System.EventHandler(this.buttonCaption_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 498);
            this.Controls.Add(this.buttonCaption);
            this.Controls.Add(this.audioSourceGroupBox);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.buttonStopStreamText);
            this.Controls.Add(this.buttonToStreamText);
            this.Controls.Add(this.buttonStopWordCopy);
            this.Controls.Add(this.buttonWordCopy);
            this.Controls.Add(this.languageGroupBox);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.realDocsButton);
            this.Controls.Add(this.labelTime);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.audioSourceButton);
            this.Controls.Add(this.stopRecordButton);
            this.Controls.Add(this.startRecordButton);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "Form1";
            this.Text = "S2T2Googledocs Word By Word";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Click += new System.EventHandler(this.Form1_Click);
            this.audioSourceGroupBox.ResumeLayout(false);
            this.audioSourceGroupBox.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.languageGroupBox.ResumeLayout(false);
            this.languageGroupBox.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Button startRecordButton;
        private System.Windows.Forms.Button stopRecordButton;
        private System.Windows.Forms.GroupBox audioSourceGroupBox;
        private System.Windows.Forms.CheckBox audioFromMicCheckBox;
        private System.Windows.Forms.Button audioSourceButton;
        private System.Windows.Forms.CheckBox audioFromSysCheckBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Label labelTime;
        private System.Windows.Forms.Button realDocsButton;
        private System.Windows.Forms.RadioButton radioButtonJustFinal;
        private System.Windows.Forms.RadioButton radioButtonWordByWord;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox languageGroupBox;
        private System.Windows.Forms.RadioButton cnenRadioButton;
        private System.Windows.Forms.RadioButton enRadioButton;
        private System.Windows.Forms.Button buttonWordCopy;
        private System.Windows.Forms.Button buttonStopWordCopy;
        private System.Windows.Forms.RichTextBox richTextBoxWordCopy;
        private System.Windows.Forms.Button buttonToStreamText;
        private System.Windows.Forms.Button buttonStopStreamText;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Button buttonCaption;
    }
}

