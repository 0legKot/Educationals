using AngleSharp.Io;
using Newtonsoft.Json.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Web;
using VDS.RDF;
using VDS.RDF.Query;
using static System.Net.WebRequestMethods;

namespace Project1.Logic {
    public class DbPediaRepository {
        private const int perPage = 5;
        private static HttpClient? client;

        private static HttpClient Client {
            get {
                if (client == null) {
                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                    httpClient.DefaultRequestHeaders.Add("Host", "dbpedia.org");
                    httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    client = httpClient;
                }
                return client;
            }
        }
        public static async Task<JObject> GetList(InstitutionType institutionType, string? name, string? type, string? country, int establishedFrom, int numberOfStudents, uint page, SortBy sortBy, bool isSortedDesc) {
            if (institutionType == InstitutionType.None) institutionType = InstitutionType.University | InstitutionType.School | InstitutionType.College;
            string query = @"
SELECT DISTINCT 
    (STR(?Name) AS ?NameClean) 
    (SAMPLE(STR(?type)) As ?Type) 
    (SAMPLE(STR(COALESCE(STR(?countryNameRaw), STR(?countryResource), """"))) AS ?Country) 
    (STR(?numberOfStudents) As ?NumberOfStudents)
    ?Id
WHERE {
{";
            if (institutionType.HasFlag(InstitutionType.University)) query += @" 
        ?Id a dbo:University .";
            query += @"} UNION {
        ";
            if (institutionType.HasFlag(InstitutionType.School)) query += @" ?Id a dbo:School .";
            query += @"} UNION {
        ";
            if (institutionType.HasFlag(InstitutionType.College)) query += @" ?Id a dbo:College .";
            query += @"
    }
    ?Id dbp:name ?Name .

    # Тип організації
    OPTIONAL { ?Id dbo:type ?typeResource . 
               OPTIONAL { ?typeResource rdfs:label ?type . }
    }

    # Пошук країни
    OPTIONAL {
        ?Id dbo:country ?countryResource.
        OPTIONAL { ?countryResource rdfs:label ?countryNameRaw . }
    }

    # Кількість студентів
    ?Id dbo:numberOfStudents ?numberOfStudents .

    FILTER (bound(?numberOfStudents) && isNumeric(?numberOfStudents))
";
            if (!string.IsNullOrEmpty(name)) query += @"FILTER(CONTAINS(STR(?Name), """ + name + @""")) ";
            if (!string.IsNullOrEmpty(country)) query += @"FILTER(CONTAINS(COALESCE(STR(?countryNameRaw), STR(?countryResource), """"), """ + country + @""")) ";
            if (!string.IsNullOrEmpty(type)) query += @"FILTER(CONTAINS(STR(?type), """ + type + @"""))";
            if (numberOfStudents > 0) query += @"FILTER(?numberOfStudents >= " + numberOfStudents + @") ";
            query += @"
}
ORDER BY ";
            query += isSortedDesc ? " DESC(" : "";
            switch (sortBy) {
                case SortBy.CountryName: query += "?Country"; break;
                case SortBy.StudentCount: query += "?numberOfStudents"; break;
                    default: query += "?Name"; break;
            }
            query += isSortedDesc ? ") " : "";   
            query += @"
LIMIT " + perPage + " OFFSET " + page * perPage;
            JObject results = null;
            try {
                results = await DoHttpQuery(query);
            } catch (Exception ex) { 
                Console.WriteLine(ex);
            }
            
            return results;
        }
        private static async Task<JObject> DoHttpQuery(string query) {
            var enc = HttpUtility.UrlEncode(query);
            var response = await Client.GetAsync($"http://dbpedia.org/sparql?query={enc}");
            Stream stream = await response.Content.ReadAsStreamAsync();
            using StreamReader input = new StreamReader(stream);
            var str = await input.ReadToEndAsync();
            JObject results = JObject.Parse(str);
            
            return results;
        }
        public static async Task<JObject?> GetEntity(string Id, Lang lang) {
            string strLang = lang == Lang.Ua ? "ua" : "en";
            string query = @"
SELECT DISTINCT 
    (STR(?Name) AS ?NameClean) 
    (SAMPLE(COALESCE(STR(?cityName), STR(?city), """")) AS ?City) 
    (SAMPLE(STR(?type)) As ?Type) 
    (SAMPLE(STR(COALESCE(STR(?countryNameRaw), STR(?countryResource), """"))) AS ?Country) 
    (STR(?numberOfStudents) As ?NumberOfStudents)
    ?Id
    ?Abstract
WHERE { 
    ?Id dbp:name ?Name .
    ?Id dbo:abstract ?Abstract

    # Тип організації
    OPTIONAL { ?Id dbo:type ?typeResource . 
               OPTIONAL { ?typeResource rdfs:label ?type . }
    }

    # Пошук країни
    OPTIONAL {
        ?Id dbo:country ?countryResource.
        OPTIONAL { ?countryResource rdfs:label ?countryNameRaw . }
    }

    # Кількість студентів
    ?Id dbo:numberOfStudents ?numberOfStudents .


    # Витягуємо місто
    OPTIONAL { ?Id dbo:city ?cityResource . 
               OPTIONAL { ?cityResource rdfs:label ?cityName . FILTER (lang(?cityName) = """ + lang + @""") }
    }
        OPTIONAL { ?Id dbp:city ?city. }

    # Фільтр для конкретного ідентифікатора
    FILTER(?Id = <" + Id + @">)
}
LIMIT 1
";
            JObject results = null;
            try {
                results = await DoHttpQuery(query);
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }

            return results;
        }
    }
}
