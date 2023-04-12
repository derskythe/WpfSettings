// ***********************************************************************
// Assembly         : PureManApplicationDeployment
// Author           : RFBomb
// Created          : 03-30-2022
//
// Last Modified By : RFBomb
// Last Modified On : 3-30-2022
// ***********************************************************************
// <copyright file="PureManClickOnce.cs" company="PureManApplicationDeployment">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Syroot.Windows.IO;
using System.Runtime.CompilerServices;

namespace PureManApplicationDeployment
{
    internal static class Extensions
    {
        /// <inheritdoc cref="CancellationToken.IsCancellationRequested"/>
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        [DebuggerHidden]
        public static bool IsCancellationRequested(this CancellationToken? token) => token.HasValue && token.Value.IsCancellationRequested;


        /// <inheritdoc cref="CancellationToken.ThrowIfCancellationRequested"/>
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        [DebuggerHidden]
        public static void ThrowIfCancellationRequested(this CancellationToken? token)
        {
            if (token.HasValue)
                token.Value.ThrowIfCancellationRequested();
        }

#if NETCOREAPP3_1_OR_GREATER
        //This does not exist in NoreCoreApp but it does in Net5
        public static Task WaitForExitAsync(this Process proc, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<object>();
            proc.Exited += (_, __) =>
            {
                proc?.WaitForExit(); //ensure process has exited!
                tcs.TrySetResult(true);
            };
            if (proc?.HasExited ?? true) return Task.CompletedTask;
            return Task.Run(() => Task.WaitAll(new Task[] { tcs.Task }, token));
        }
#endif

    }
}