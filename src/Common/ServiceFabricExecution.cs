using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
	public static class ServiceFabricExecution
	{
		public static async Task RunAsync(Func<CancellationToken, Task> f, CancellationToken cancellationToken, ILogger logger)
		{
			try
			{
				await f(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
			}
            catch (Exception e) when (cancellationToken.IsCancellationRequested)
            {
                if (e is OperationCanceledException)
                {
                    logger.LogInformation(e, "RunAsync Canceled");
                    throw;
                }

                if (IsCancellation(e))
                {
                    logger.LogWarning(e, "RunAsync Canceled");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                logger.LogError(e, "Exception during shutdown. Exception of unexpected type");
                cancellationToken.ThrowIfCancellationRequested();
            }
			catch (Exception e) when (IsCancellation(e))
			{
				logger.LogError(e, "Cancellation Exception");
				cancellationToken.ThrowIfCancellationRequested();
				throw;
			}
			catch (Exception e)
			{
				logger.LogError(e, "Unhandled Exception");
				throw;
			}
		}

		public static async Task WhileAsync(
			Func<CancellationToken, Task> f, string functionName, CancellationToken cancellationToken, ILogger logger, int retryDelaySeconds = 60)
		{
			logger.LogInformation($"Run {functionName}");
			var exceptionRetryDelay = TimeSpan.FromSeconds(retryDelaySeconds);

			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var retryDelay = TimeSpan.Zero;
				try
				{
					await f(cancellationToken);
					cancellationToken.ThrowIfCancellationRequested();
				}
                catch (Exception e) when (cancellationToken.IsCancellationRequested)
                {
                    if (e is OperationCanceledException)
                    {
                        logger.LogInformation(e, "RunAsync Canceled");
                        throw;
                    }

                    if (IsCancellation(e))
                    {
                        logger.LogWarning(e, "RunAsync Canceled");
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    logger.LogError(e, "Exception during shutdown. Exception of unexpected type");
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception e) when (IsCancellation(e))
				{
					logger.LogInformation(e, $"Cancellation Exception in {functionName}");
					cancellationToken.ThrowIfCancellationRequested();
				}
				catch (FabricTransientException e)
				{
					logger.LogInformation(e, $"FabricTransientException in {functionName}");
					retryDelay = exceptionRetryDelay;
				}
				catch (FabricNotPrimaryException e)
				{
					logger.LogInformation(e, "Service fabric is not primary");
					return;
				}
				catch (FabricException e)
				{
					logger.LogInformation(e, $"Fabric Exception in {functionName}");
					throw;
				}
				catch (Exception e)
				{
					logger.LogError(e, $"Application Exception in {functionName}");
					retryDelay = exceptionRetryDelay;
				}

				await Task.Delay(retryDelay, cancellationToken);
			}
		}

		static readonly HashSet<Type> CancellationExceptions = new HashSet<Type>
		{
			typeof(OperationCanceledException),
			typeof(TaskCanceledException),
			typeof(TimeoutException)
		};

		static bool IsCancellation(Exception exception)
		{
			if (exception is AggregateException aggregateException)
			{
				return aggregateException.InnerExceptions.Any(IsCancellation);
			}

			return CancellationExceptions.Contains(exception.GetType());
		}
	}
}
