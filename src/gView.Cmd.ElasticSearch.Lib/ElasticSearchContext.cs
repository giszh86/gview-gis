﻿using Nest;

namespace gView.Cmd.ElasticSearch.Lib;

class ElasticSearchContext : IDisposable
{
    private ElasticClient _client;
    private string _defalutIndex = String.Empty;

    public ElasticSearchContext(string url = "http://localhost:9200", string defaultIndex = "",
                                string? proxyUri = "", string? proxyUsername = "", string? proxyPassword = "",
                                string? basicAuthUser = "", string? basicAuthPassword = "")
    {
        var node = new Uri(url);

        var settings = new ConnectionSettings(node);
        settings.DefaultIndex(_defalutIndex = defaultIndex);

        if (!String.IsNullOrEmpty(proxyUri))
        {
            settings.Proxy(new Uri(proxyUri), proxyUsername, proxyPassword);
        }
        if (!String.IsNullOrEmpty(basicAuthUser) && !String.IsNullOrEmpty(basicAuthPassword))
        {
            settings.BasicAuthentication(basicAuthUser, basicAuthPassword);
        }

        _client = new ElasticClient(settings);
    }

    #region IDisposable

    public void Dispose()
    {
    }

    #endregion

    public string LastErrorMessage { get; private set; } = "";

    #region Create/Delete Index

    public bool CreateIndex<T>(string indexName = "")
        where T : class
    {
        indexName = CurrentIndexName(indexName);
        if (String.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("No Index name");
        }

        var createIndexResult = _client.Indices.Create(indexName,
            index => index.Map(x => x.AutoMap()));

        if (createIndexResult.OriginalException != null)
        {
            LastErrorMessage = createIndexResult.OriginalException.Message;
        }

        return createIndexResult.IsValid;
    }

    public bool Map<T>(string indexName = "")
        where T : class
    {
        indexName = CurrentIndexName(indexName);
        if (String.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("No Index name");
        }

        var mapResult = _client.Map<T>(c => c
            .AutoMap()
            .Index(indexName));

        if (mapResult.OriginalException != null)
        {
            LastErrorMessage = mapResult.OriginalException.Message;
        }

        return mapResult.IsValid;
    }

    public bool DeleteIndex(string indexName = "")
    {
        indexName = CurrentIndexName(indexName);
        if (String.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("No Index name");
        }

        var result = _client.Indices.Delete(indexName);

        if (result.OriginalException != null)
        {
            LastErrorMessage = result.OriginalException.Message;
        }

        return result.IsValid;
    }

    #endregion

    #region Index

    public bool Index<T>(T document, string indexName = "")
        where T : class
    {
        indexName = CurrentIndexName(indexName);
        if (String.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("No Index name");
        }

        var response = _client.Index<T>(document, idx => idx.Index(indexName));

        if (response.OriginalException != null)
        {
            LastErrorMessage = response.OriginalException.Message;
        }

        return response.IsValid;
    }

    public bool IndexMany<T>(T[] documents, string indexName = "")
        where T : class
    {
        indexName = CurrentIndexName(indexName);
        if (String.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("No Index name");
        }

        var response = _client.IndexMany<T>(documents, indexName);

        if (response.OriginalException != null)
        {
            LastErrorMessage = response.OriginalException.Message;
        }

        return response.IsValid;
    }

    public bool IndexManyPro<T>(T[] documents, string indexName = "", int maxTries = 5)
        where T : class
    {
        int tries = 0;

        while (true)
        {
            if (IndexMany<T>(documents, indexName))
            {
                return true;
            }

            tries++;
            if (maxTries > 5)
            {
                return false;
            }

            Console.Write("...retry");
            System.Threading.Thread.Sleep(3000);
        }
    }

    public bool Remove<T>(string id, string indexName = "")
        where T : class, new()
    {
        indexName = CurrentIndexName(indexName);
        if (String.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("No Index name");
        }

        var response = _client.Delete<T>(id, idx => idx.Index(indexName));

        if (response.OriginalException != null)
        {
            LastErrorMessage = response.OriginalException.Message;
        }

        // ToDo: Check Result
        return response.IsValid;
    }

    public bool RemoveMany<T>(IEnumerable<T> objects, string indexName = "")
        where T : class, new()
    {
        indexName = CurrentIndexName(indexName);
        if (String.IsNullOrWhiteSpace(indexName))
        {
            throw new ArgumentException("No Index name");
        }

        var response = _client.DeleteMany<T>(objects, indexName);

        if (response.OriginalException != null)
        {
            LastErrorMessage = response.OriginalException.Message;
        }

        return response.Errors == false;
    }

    #endregion

    #region Queries

    public IEnumerable<T> QueryAll<T>(string indexName = "")
        where T : class
    {
        indexName = CurrentIndexName(indexName);

        List<T> ret = new List<T>();
        int pos = 0, size = 100;
        while (true)
        {
            var result = _client.Search<T>(s => s.From(pos).Size(size));

            var count = result.Documents != null ? result.Documents.Count() : 0;

            pos += result.Documents?.Count() ?? 0;
            if (count > 0)
            {
                ret.AddRange(result.Documents!);
            }

            if (count < size)
            {
                break;
            }
        }
        return ret;
    }

    public IEnumerable<T> Query<T>(SearchFilter filter, int max = int.MaxValue, string indexName = "")
        where T : class
    {
        return Query<T>(new SearchFilter[] { filter }, max, indexName);
    }

    public IEnumerable<T> Query<T>(SearchFilter[] filters, int max = int.MaxValue, string indexName = "")
        where T : class
    {
        indexName = CurrentIndexName(indexName);

        List<T> ret = new List<T>();
        int pos = 0, size = 10000;
        while (true)
        {
            var request = new SearchRequest<T>
            {
                From = pos,
                Size = Math.Min(max, size)
            };
            AppendFilters<T>(request, filters);
            var result = _client.Search<T>(request);

            var count = result.Documents != null ? result.Documents.Count() : 0;

            pos += result.Documents?.Count() ?? 0;
            if (count > 0)
            {
                ret.AddRange(result.Documents!);
            }

            if (count < size || ret.Count() >= max)
            {
                break;
            }
        }
        return ret;
    }

    #endregion

    #region Aggregate 

    public Aggragtion[] Aggregate<T>(string field, SearchFilter[]? filters = null, string function = "count", string indexName = "")
        where T : class
    {
        switch (function.ToLower())
        {
            case "count":
                return CountAggregation<T>(field, filters, indexName);
            case "sum":
                return SumAggregation<T>(field, filters, indexName);
            case "avg":
                return AverageAggregation<T>(field, filters, indexName);
            case "min":
                return MinAggregation<T>(field, filters, indexName);
            case "max":
                return MaxAggregation<T>(field, filters, indexName);
        }

        throw new ArgumentException("Unsupported function: " + function);
    }

    private Aggragtion[] CountAggregation<T>(string field, SearchFilter[]? filters = null, string indexName = "")
        where T : class
    {
        indexName = CurrentIndexName(indexName);

        var request = new SearchRequest<T>
        {
            Size = 0,
            Aggregations = new TermsAggregation("agg_" + field) { Field = field, Size = 1000 }
        };
        AppendFilters<T>(request, filters);
        var result = _client.Search<T>(request);
        var aggHelper = result.Aggregations.Terms("agg_" + field);

        List<Aggragtion> ret = new List<Aggragtion>();
        foreach (var bucket in aggHelper.Buckets)
        {
            var aggregation = new Aggragtion()
            {
                Field = field,
                Result = bucket.DocCount != null ? (long)bucket.DocCount : 0,
                Element = new Dictionary<string, object>()
            };
            aggregation.Element.Add(field, bucket.Key);
            ret.Add(aggregation);
        }
        return ret.ToArray();
    }

    private Aggragtion[] SumAggregation<T>(string field, SearchFilter[]? filters = null, string indexName = "")
        where T : class
    {
        indexName = CurrentIndexName(indexName);

        var request = new SearchRequest<T>
        {
            Size = 0,
            Aggregations = new SumAggregation("agg_" + field, field)
        };
        AppendFilters<T>(request, filters);
        var result = _client.Search<T>(request);
        var aggHelper = result.Aggregations.Sum("agg_" + field);

        List<Aggragtion> ret = new List<Aggragtion>();

        var aggregation = new Aggragtion()
        {
            Field = field,
            Result = aggHelper.Value != null ? (double)aggHelper.Value : 0,
            Element = new Dictionary<string, object>()
        };
        aggregation.Element.Add(field, aggHelper.Value!);
        ret.Add(aggregation);

        return ret.ToArray();
    }

    private Aggragtion[] AverageAggregation<T>(string field, SearchFilter[]? filters = null, string indexName = "")
        where T : class
    {
        indexName = CurrentIndexName(indexName);

        var request = new SearchRequest<T>
        {
            Size = 0,
            Aggregations = new AverageAggregation("agg_" + field, field)
        };
        AppendFilters<T>(request, filters);
        var result = _client.Search<T>(request);
        var aggHelper = result.Aggregations.Average("agg_" + field);

        List<Aggragtion> ret = new List<Aggragtion>();

        var aggregation = new Aggragtion()
        {
            Field = field,
            Result = aggHelper.Value != null ? (double)aggHelper.Value : 0,
            Element = new Dictionary<string, object>()
        };
        aggregation.Element.Add(field, aggHelper.Value!);
        ret.Add(aggregation);

        return ret.ToArray();
    }

    private Aggragtion[] MinAggregation<T>(string field, SearchFilter[]? filters = null, string indexName = "")
        where T : class
    {
        indexName = CurrentIndexName(indexName);

        var request = new SearchRequest<T>
        {
            Size = 0,
            Aggregations = new MinAggregation("agg_" + field, field)
        };
        AppendFilters<T>(request, filters);
        var result = _client.Search<T>(request);
        var aggHelper = result.Aggregations.Average("agg_" + field);

        List<Aggragtion> ret = new List<Aggragtion>();

        var aggregation = new Aggragtion()
        {
            Field = field,
            Result = aggHelper.Value != null ? (double)aggHelper.Value : 0,
            Element = new Dictionary<string, object>()
        };
        aggregation.Element.Add(field, aggHelper.Value!);
        ret.Add(aggregation);

        return ret.ToArray();
    }

    private Aggragtion[] MaxAggregation<T>(string field, SearchFilter[]? filters = null, string indexName = "")
       where T : class
    {
        indexName = CurrentIndexName(indexName);

        var request = new SearchRequest<T>
        {
            Size = 0,
            Aggregations = new MaxAggregation("agg_" + field, field)
        };
        AppendFilters<T>(request, filters);
        var result = _client.Search<T>(request);
        var aggHelper = result.Aggregations.Average("agg_" + field);

        List<Aggragtion> ret = new List<Aggragtion>();

        var aggregation = new Aggragtion()
        {
            Field = field,
            Result = aggHelper.Value != null ? (double)aggHelper.Value : 0,
            Element = new Dictionary<string, object>()
        };
        aggregation.Element.Add(field, aggHelper.Value!);
        ret.Add(aggregation);

        return ret.ToArray();
    }

    #endregion

    #region Helper

    private void AppendFilters<T>(SearchRequest<T> request, SearchFilter[]? filters)
    {
        if (filters != null)
        {
            foreach (var filter in filters)
            {
                if (filter.Value == null)
                {
                    continue;
                }

                //var qsq = new TermQuery { Field = new Field(filter.Field), Value = filter.Value };
                //MatchQuery qsq=new MatchQuery { Field = filter.Field,  Query = new  };
                QueryStringQuery qsq = new QueryStringQuery { DefaultField = filter.Field, Query = filter.Value.ToString() };  // Fuzzy
                if (request.Query == null)
                {
                    request.Query = qsq;
                }
                else
                {
                    request.Query &= qsq;
                }
            }
        }
    }

    private string CurrentIndexName(string indexName)
    {
        if (String.IsNullOrWhiteSpace(indexName))
        {
            indexName = _defalutIndex;
        }

        return indexName;
    }

    #endregion

    #region Classes

    public class Aggragtion
    {
        public string Field { get; set; } = "";
        public double Result { get; set; }
        public Dictionary<string, object>? Element { get; set; }
    }

    public class SearchFilter
    {
        public string Field { get; set; } = "";
        public object? Value { get; set; }
        public string Operator { get; set; } = "";
    }

    #endregion
}
