// The MIT License (MIT)
// 
// Copyright (c) 2015-2024 Rasmus Mikkelsen
// https://github.com/eventflow/EventFlow
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using EventFlow.Configuration.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EventFlow.AspNetCore.Configuration
{
    public class EventFlowJsonOptionsMvcConfiguration
#if NETSTANDARD2_0
        : IConfigureOptions<MvcJsonOptions>
#endif
#if (NETCOREAPP3_0 || NETCOREAPP3_1)
        : IConfigureOptions<MvcNewtonsoftJsonOptions>
#endif
    {
        private readonly IJsonOptions _jsonOptions;

        public EventFlowJsonOptionsMvcConfiguration(IJsonOptions jsonOptions)
        {
            _jsonOptions = jsonOptions;
        }

#if NETSTANDARD2_0
        public void Configure(MvcJsonOptions options)
        {
            _jsonOptions.Apply(options.SerializerSettings);
        }
#endif
#if (NETCOREAPP3_0 || NETCOREAPP3_1)
        public void Configure(MvcNewtonsoftJsonOptions options)
        {
            _jsonOptions.Apply(options.SerializerSettings);
        }
#endif
    }
}
