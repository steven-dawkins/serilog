﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Elasticsearch.Net.Connection;
using Elasticsearch.Net.Connection.Configuration;
using FakeItEasy;
using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.ElasticSearch;
using NUnit.Framework;

namespace Serilog.Sinks.Elasticsearch.Tests
{
    [TestFixture]
    public class RealExceptionNoSerializerTests : ElasticsearchSinkTestsBase
    {
        [Test]
        public async Task WhenPassingASerializer_ShouldExpandToJson()
        {
            try
            {
                await new HttpClient().GetStringAsync("http://i.do.not.exist");
            }
            catch (Exception e)
            {
                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var messageTemplate = "{Song}++";
                var template = new MessageTemplateParser().Parse(messageTemplate);
                using (var sink = new ElasticsearchSink(_options)
                    )
                {
                    var properties = new List<LogEventProperty>
					{
						new LogEventProperty("Song", new ScalarValue("New Macabre")),
						new LogEventProperty("Complex", new ScalarValue(new { A  = 1, B = 2}))
					};
                    var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null, template, properties);
                    sink.Emit(logEvent);
                    logEvent = new LogEvent(timestamp.AddDays(2), LogEventLevel.Information, e, template, properties);
                    sink.Emit(logEvent);
                }
                _seenHttpPosts.Should().NotBeEmpty().And.HaveCount(1);
                var json = _seenHttpPosts.First();
                var bulkJsonPieces = json.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                bulkJsonPieces.Should().HaveCount(4);
                bulkJsonPieces[0].Should().Contain(@"""_index"":""logstash-2013.05.28");
                bulkJsonPieces[1].Should().Contain("New Macabre");
                bulkJsonPieces[1].Should().NotContain("Properties\"");
                bulkJsonPieces[2].Should().Contain(@"""_index"":""logstash-2013.05.30");

                //We have no serializer associated with the sink so we expect the forced ToString() of scalar values
                bulkJsonPieces[3].Should().Contain("Complex\":\"{");
                bulkJsonPieces[3].Should().Contain("exception\":\"System.Net.Http.HttpRequestException: An error");
            }
        }
    }

}
