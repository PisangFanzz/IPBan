﻿/*
MIT License

Copyright (c) 2019 Digital Ruby, LLC - https://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Threading;
using System.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace DigitalRuby.IPBanCore
{
    public class IPBanServiceRunner : BackgroundService
    {
        private readonly Func<Task> onStart;
        private readonly Func<Task> onStop;

        private IHost host;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="onStart">Action to execute on start</param>
        /// <param name="onStop">Action to execute on stop</param>
        private IPBanServiceRunner(Func<Task> onStart, Func<Task> onStop)
        {
            Logger.Warn("Initializing service");
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            OSUtility.Instance.AddAppDomainExceptionHandlers(AppDomain.CurrentDomain);
            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<IPBanServiceRunner>(provider => this);
                });

            this.onStart = onStart;
            this.onStop = onStop;
            hostBuilder.UseWindowsService();
            hostBuilder.UseSystemd();
            hostBuilder.UseConsoleLifetime();
            host = hostBuilder.Build();
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public override void Dispose()
        {
            if (host != null)
            {
                Logger.Warn("Disposing service");
                base.Dispose();
                host.Dispose();
                host = null;
            }
        }

        /// <summary>
        /// Run the service
        /// </summary>
        /// <param name="cancelToken">Cancel token</param>
        /// <returns>Task</returns>
        public Task RunAsync(CancellationToken cancelToken = default)
        {
            Logger.Warn("Preparing to run service");
            return host.RunAsync(cancelToken);
        }

        /// <summary>
        /// Run service helper method
        /// </summary>
        /// <param name="args">Args</param>
        /// <param name="onStart">Start</param>
        /// <param name="onStop">Stop</param>
        /// <param name="cancelToken">Cancel token</param>
        /// <returns>Task</returns>
#pragma warning disable IDE0060 // Remove unused parameter
        public static async Task MainService(string[] args, Func<Task> onStart, Func<Task> onStop = null, CancellationToken cancelToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            try
            {
                using IPBanServiceRunner runner = new IPBanServiceRunner(onStart, onStop);
                await runner.RunAsync(cancelToken);
            }
            catch (Exception ex)
            {
                ExtensionMethods.FileWriteAllTextWithRetry(System.IO.Path.Combine(AppContext.BaseDirectory, "service_error.txt"), ex.ToString());
                Logger.Fatal("Fatal error running service", ex);
            }
        }

        /// <inheritdoc />
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.Warn("Starting service");
            await base.StartAsync(cancellationToken);
            if (onStart != null)
            {
                await onStart();
            }
        }

        /// <inheritdoc />
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.Warn("Stopping service");
            await base.StopAsync(cancellationToken);
            if (onStop != null)
            {
                await onStop();
            }
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.Warn("Running service");
            await Task.Delay(-1, stoppingToken);
        }
    }
}
