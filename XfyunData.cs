using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdamS2T2Docs
{
    public class XfyunData
    {
        public int seg_id { get; set; } // 转写结果序号	从0开始
        public Cn cn { get; set; }
        public bool ls { get; set; }
    }

    public class Cn
    {
        public St st { get; set; }
    }

    public class St
    {
        public Rt[] rt { get; set; }
        public string bg { get; set; } //句子在整段语音中的开始时间，单位毫秒(ms)	中间结果的bg为准确值
        public string type { get; set; }// 结果类型标识	0-最终结果；1-中间结果
        public string ed { get; set; } //句子在整段语音中的结束时间，单位毫秒(ms)	中间结果的ed为0
    }

    public class Rt
    {
        public W[] ws { get; set; }
    }

    public class W
    {
        public Cw[] cw { get; set; }
        public int wb { get; set; } //词在本句中的开始时间，单位是帧，1帧=10ms 即词在整段语音中的开始时间为(bg+wb*10)ms 中间结果的 wb 为 0
        public int we { get; set; } // 词在本句中的结束时间，单位是帧，1帧=10ms 即词在整段语音中的结束时间为(bg+we*10)ms 中间结果的 we 为 0
    }

    public class Cw
    {
        public float sc { get; set; }
        public string w { get; set; }//词识别结果
        public string og { get; set; }
        public string wp { get; set; } // 词标识	n-普通词；s-顺滑词（语气词）；p-标点
        public string rl { get; set; } //分离的角色编号，需开启角色分离的功能才返回对应的分离角色编号	取值正整数
        public int wb { get; set; } //词在本句中的开始时间，单位是帧，1帧=10ms 即词在整段语音中的开始时间为(bg+wb*10)ms 中间结果的 wb 为 0
        public float wc { get; set; }
        public int we { get; set; } //词在本句中的结束时间，单位是帧，1帧=10ms 即词在整段语音中的结束时间为(bg+we*10)ms 中间结果的 we 为 0
    }

}
