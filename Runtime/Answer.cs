namespace UnityRenderStreamingWebService
{
    public class Answer
    {
        public string sdp { get; set; }
        public long datetime { get; set; }
        public Answer() { }
        public Answer(string sdp, long datetime)
        {
            this.sdp = sdp;
            this.datetime = datetime;
        }
    }
}