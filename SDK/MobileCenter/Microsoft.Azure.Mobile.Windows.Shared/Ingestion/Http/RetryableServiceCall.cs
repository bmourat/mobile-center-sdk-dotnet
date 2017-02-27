﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Mobile.Ingestion.Models;
using Microsoft.Rest;

namespace Microsoft.Azure.Mobile.Ingestion.Http
{
    public class RetryableServiceCall : ServiceCallDecorator
    {
        private static readonly Random Random = new Random();
        private static readonly SemaphoreSlim RandomMutex = new SemaphoreSlim(1, 1);

        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private int _retryCount;
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);

        private readonly TimeSpan[] _retryIntervals = { TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(.5), TimeSpan.FromMinutes(20) };

        public override CancellationToken CancellationToken => _tokenSource.Token;

        public RetryableServiceCall(IServiceCall decoratedApi) : base(decoratedApi)
        {
        }

        ///<exception cref="IngestionException"/>
        public async Task RunWithRetriesAsync()
        {
            await _mutex.WaitAsync();
            try
            {
                _tokenSource = new CancellationTokenSource();
                await RunWithRetriesAsyncHelper();
            }
            finally
            {
                _mutex.Release();
            }
        }

        ///<exception cref="IngestionException"/>
        private async Task RunWithRetriesAsyncHelper()
        {
            try
            {
                await Ingestion.ExecuteCallAsync(this);
            }
            catch (IngestionException e)
            {
                if (!e.IsRecoverable || _retryCount >= _retryIntervals.Length)
                {
                    throw;
                }

                var delayMilliseconds = (int)(_retryIntervals[_retryCount++].TotalMilliseconds / 2.0);
                delayMilliseconds += await GetRandomIntAsync(delayMilliseconds);
                var message = $"Try #{_retryCount} failed and will be retried in {delayMilliseconds} ms";
                MobileCenterLog.Warn(MobileCenterLog.LogTag, message, e);
                await Task.Delay(delayMilliseconds);
                _tokenSource.Token.ThrowIfCancellationRequested();
                await RunWithRetriesAsyncHelper();
            }
        }

        private static async Task<int> GetRandomIntAsync(int max)
        {
            await RandomMutex.WaitAsync();
            var randomInt = Random.Next(max);
            RandomMutex.Release();
            return randomInt;
        }

        public override void Cancel()
        {
            _tokenSource?.Cancel();
        }

        public override void Execute()
        {
            RunWithRetriesAsync().ContinueWith(completedTask =>
            {
                if (completedTask.IsFaulted)
                {
                    ServiceCallFailedCallback?.Invoke(completedTask.Exception?.InnerException as IngestionException);
                }
                else
                {
                    ServiceCallSucceededCallback?.Invoke();
                }
            });
        }

    }
}