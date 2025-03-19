using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace IdlePlus.Utilities
{
    /// <summary>
    /// Generic queue for asynchronous processing of items.
    /// </summary>
    /// <typeparam name="T">The type of items in the queue.</typeparam>
    public abstract class WebhookQueue<T>
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly SemaphoreSlim _processingLock = new SemaphoreSlim(1, 1);
        private volatile bool _isProcessing;
        private readonly int _maxConsecutiveErrors;
        private int _consecutiveErrors;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the WebhookQueue class.
        /// </summary>
        /// <param name="maxConsecutiveErrors">Maximum number of consecutive errors before pausing processing.</param>
        protected WebhookQueue(int maxConsecutiveErrors = 5)
        {
            _maxConsecutiveErrors = maxConsecutiveErrors;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Gets the number of items in the queue.
        /// </summary>
        public int Count => _queue.Count;

        /// <summary>
        /// Adds an item to the queue for processing.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        public void Enqueue(T item)
        {
            if (item == null)
            {
                IdleLog.Error("[WebhookQueue] Cannot enqueue null item");
                return;
            }

            _queue.Enqueue(item);
            IdleLog.Debug($"[WebhookQueue] Item added to queue. Queue size: {_queue.Count}");
            StartProcessingIfNotRunning();
        }

        /// <summary>
        /// Starts the queue processor if it is not already running.
        /// </summary>
        private async void StartProcessingIfNotRunning()
        {
            // Use SemaphoreSlim to ensure only one thread can check and set _isProcessing
            bool shouldProcess = false;
            
            try
            {
                await _processingLock.WaitAsync();
                
                if (_isProcessing) return;
                _isProcessing = true;
                shouldProcess = true;
                
                // Reset cancellation token if it was cancelled
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = new CancellationTokenSource();
                }
            }
            catch (Exception ex)
            {
                IdleLog.Error($"[WebhookQueue] Error in StartProcessingIfNotRunning: {ex.Message}");
                _isProcessing = false;
            }
            finally
            {
                _processingLock.Release();
            }

            if (shouldProcess)
            {
                try
                {
                    await ProcessQueueAsync(_cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    IdleLog.Debug("[WebhookQueue] Queue processing was cancelled");
                }
                catch (Exception ex)
                {
                    IdleLog.Error($"[WebhookQueue] Unhandled error in queue processing: {ex.Message}");
                }
                
                await _processingLock.WaitAsync();
                try
                {
                    _isProcessing = false;
                }
                finally
                {
                    _processingLock.Release();
                }
            }
        }

        /// <summary>
        /// Continuously processes the queue asynchronously.
        /// </summary>
        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            IdleLog.Debug("[WebhookQueue] Started processing queue");
            
            while (!_queue.IsEmpty && !cancellationToken.IsCancellationRequested)
            {
                // Check if we've hit the consecutive error limit
                if (_consecutiveErrors >= _maxConsecutiveErrors)
                {
                    IdleLog.Error($"[WebhookQueue] Pausing processing after {_consecutiveErrors} consecutive errors. Will retry in 30 seconds.");
                    try
                    {
                        await Task.Delay(30000, cancellationToken); // Wait 30 seconds
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    _consecutiveErrors = 0;  // Reset the counter and try again
                }

                if (_queue.TryDequeue(out var item))
                {
                    try
                    {
                        await ProcessItemAsync(item);
                        _consecutiveErrors = 0; // Reset on success
                    }
                    catch (Exception ex)
                    {
                        _consecutiveErrors++;
                        IdleLog.Error($"[WebhookQueue] Error processing item: {ex.Message}");
                        
                        try
                        {
                            // Exponential backoff
                            int delay = 100 * (int)Math.Pow(2, Math.Min(_consecutiveErrors, 6)); // Cap to avoid excessive waits
                            await Task.Delay(delay, cancellationToken);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    try
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
            
            IdleLog.Debug($"[WebhookQueue] Finished processing queue. Remaining items: {_queue.Count}");
        }

        /// <summary>
        /// Cancels all ongoing queue processing.
        /// </summary>
        public void CancelProcessing()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                IdleLog.Debug("[WebhookQueue] Processing cancelled");
            }
            catch (Exception ex)
            {
                IdleLog.Error($"[WebhookQueue] Error cancelling processing: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a single item from the queue.
        /// </summary>
        /// <param name="item">The item to process.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected abstract Task ProcessItemAsync(T item);
    }
}