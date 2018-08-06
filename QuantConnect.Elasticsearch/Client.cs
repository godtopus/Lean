using Nest;

namespace QuantConnect.Elasticsearch
{
    public class Client
    {
        public static ElasticClient ElasticClient { get; } = new ElasticClient();
    }
}
