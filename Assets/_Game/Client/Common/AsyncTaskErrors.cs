using System;
using UnityEngine;

namespace Game.Client.Common
{
    /// <summary>Reports unexpected failures at UI and scene event entry points.</summary>
    public static class AsyncTaskErrors
    {
        public static void Report(Exception failure)
        {
            if (failure is not OperationCanceledException)
                Debug.LogException(failure);
        }
    }
}
