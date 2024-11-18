using VDS.RDF.Query;

namespace Project1.Components {
    public class DbPediaClient {
        private static SparqlQueryClient? client;

        private static SparqlQueryClient Client {
            get {
                if (client == null) {
                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                    httpClient.DefaultRequestHeaders.Add("Host", "dbpedia.org");
                    httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    client = new SparqlQueryClient(httpClient, new Uri("http://dbpedia.org/sparql"));
                }
                return client;
            }
        }
        public static async Task<SparqlResultSet> Get(string query) {
            
            SparqlResultSet results = await Client.QueryWithResultSetAsync("select distinct ?Concept where {[] a ?Concept} LIMIT 10");
            return results;
        }

    }
}
