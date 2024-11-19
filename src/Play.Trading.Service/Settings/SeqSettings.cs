namespace Play.Trading.Service.Settings 
{
    public class SeqSettings
    {
        /// <summary>
        /// Seq host endpoint.
        /// </summary>
        public string Host { get; init; }
        
        /// <summary>
        /// The port to send logs to.
        /// </summary>
        public string Port { get; init; }

        /// <summary>
        /// The connection url string.
        /// </summary>
        public string ServerUrl 
            => $"http://{Host}:{Port}";
    }
}