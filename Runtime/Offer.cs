namespace UnityRenderStreamingWebService
{
    public class Offer
    {
        public string sdp { get; set; }
        public long datetime { get; set; }
        public bool polite { get; set; }
        public Offer() { }
        public Offer(string sdp, long datetime, bool polite)
        {
            this.sdp = sdp;
            this.datetime = datetime;
            this.polite = polite;
        }

    }
}