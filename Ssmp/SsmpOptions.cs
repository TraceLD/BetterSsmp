namespace Ssmp
{
    public class SsmpOptions
    {
        public int MessageQueueLimit { get; set; }
        public string IpAddress { get; set; } = null!;
        public int Port { get; set; }
    }
}