// ***********************************************************************
// Assembly         : PureManApplicationDeployment
// Author           : Skif
// Created          : 02-04-2021
//
// Last Modified By : Skif
// Last Modified On : 02-04-2021
// ***********************************************************************
// <copyright file="ClickOnceDeploymentException.cs" company="PureManApplicationDeployment">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************


using System;
using PureManApplicationDeployment.Models;

namespace PureManApplicationDeployment.Helpers;


/// <summary>
/// Class ClickOnceDeploymentException.
/// Implements the <see cref="System.Exception" />
/// </summary>
/// <seealso cref="System.Exception" />
public sealed class ClickOnceDeploymentException : Exception
{
    public ClickOnceResult Code { get; init; }
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickOnceDeploymentException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ClickOnceDeploymentException(ClickOnceResult code, string message) : base(message)
    {
    }
}
