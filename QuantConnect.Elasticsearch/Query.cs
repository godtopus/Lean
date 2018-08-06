using Nest;
using System;

namespace QuantConnect.Elasticsearch
{
    class Query
    {
        public static ISearchResponse<TimeSeries> Search(string name, DateTime time, DateTime endTime)
        {
            var searchResponse = Client.ElasticClient.Search<TimeSeries>(s => s
                .From(0)
                .Size(1)
                .Query(q => q
                    .Match(m => m
                        .Field(f => f.Name)
                        .Query(name)
                    )
                )
                .Query(q => q
                    .Nested(n => n
                        .Path(p => p.Series)
                        .Query(nq => nq
                            .DateRange(m => m
                                .Field(f => f.Series)
                                .GreaterThanOrEquals(time.ToUniversalTime())
                                .LessThan(endTime.ToUniversalTime())
                            )
                        )
                    )
                )

                .Query(q => q
                    .Nested(n => n
                        .Path(p => p.Series)
                        .Query(nq => nq
                            .DateRange(m => m
                                .Field(f => f.Series)
                                .GreaterThan(time.ToUniversalTime())
                                .LessThanOrEquals(endTime.ToUniversalTime())
                            )
                        )
                    )
                )
                .Sort(so => so
                    .Field(f => f
                        .Field(p => p.Series[0].Time)
                        .Ascending()
                        .Nested(n => n
                            .Path(p => p.Series))
                    )
                )
            );

            return searchResponse;
        }
    }
}
