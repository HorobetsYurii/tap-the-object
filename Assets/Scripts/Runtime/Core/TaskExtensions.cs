using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TapTheObject.Core
{
    internal static class TaskExtensions
    {
        /// <summary>
        /// Starts a task that is deliberately not awaited, reporting failures instead of leaving them
        /// unobserved.
        /// </summary>
        internal static async void Forget(this Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
