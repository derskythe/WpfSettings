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


using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PureManApplicationDeployment;


internal static class Extensions
{
    public static Task WaitForExitAsync(this Process proc, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<object>();

        proc.Exited += (_, __) =>
        {
            proc.WaitForExit(); //ensure process has exited!
            tcs.TrySetResult(true);
        };

        if (proc?.HasExited ?? true)
        {
            return Task.CompletedTask;
        }

        return Task.Run
            (
             () => Task.WaitAll
                 (
                  new Task[]
                  {
                      tcs.Task,
                  },
                  cancellationToken
                 ),
             cancellationToken
            );
    }
}
