namespace UnityRenderStreamingWebService
{
    public class Candidate
    {
        public string candidate { get; set; }
        public int sdpMLineIndex { get; set; }
        public string sdpMid { get; set; }
        public long datetime { get; set; }
        public Candidate() { }
        public Candidate(string candidate, int sdpMLineIndex, string sdpMid, long datetime)
        {
            this.candidate = candidate;
            this.sdpMLineIndex = sdpMLineIndex;
            this.sdpMid = sdpMid;
            this.datetime = datetime;
        }
    }
}