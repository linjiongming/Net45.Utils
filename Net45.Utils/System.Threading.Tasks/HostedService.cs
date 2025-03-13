using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Threading.Tasks
{
    public abstract class HostedService : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        protected CancellationTokenSource CTS { get; private set; }

        protected abstract Task StartAsync();

        protected abstract Task StopAsync();

        public async Task StartAsync(CancellationToken cancellation)
        {
            CTS = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            bool releaseGuard = false;
            try
            {
                await _semaphore.WaitAsync(cancellation).ConfigureAwait(false);
                releaseGuard = true;
                await StartAsync();
            }
            catch (OperationCanceledException) { /*ignore*/ }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message, ex);
            }
            finally
            {
                if (releaseGuard)
                {
                    _semaphore.Release();
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellation)
        {
            bool releaseGuard = false;
            try
            {
                await _semaphore.WaitAsync(cancellation).ConfigureAwait(false);
                releaseGuard = true;
                await StopAsync();
            }
            catch (OperationCanceledException) { /*ignore*/ }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message, ex);
            }
            finally
            {
                if (releaseGuard)
                {
                    _semaphore.Release();
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    if (CTS != null)
                    {
                        CTS.Cancel();
                        CTS.Dispose();
                        CTS = null;
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~HostedService() {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
