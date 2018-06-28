﻿//  <copyright file="ICodeZeroActionResultWrapperFactory.cs" project="CodeZero.AspNetCore" solution="CodeZero">
//      Copyright (c) 2018 CodeZero Framework.  All rights reserved.
//  </copyright>
//  <author>Nasr Aldin M.</author>
//  <email>nasr2ldin@gmail.com</email>
//  <website>https://nasraldin.com/codezero</website>
//  <github>https://nasraldin.github.io/CodeZero</github>
//  <date>01/01/2018 01:00 AM</date>
using CodeZero.Dependency;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodeZero.AspNetCore.Mvc.Results.Wrapping
{
    public interface ICodeZeroActionResultWrapperFactory : ITransientDependency
    {
        ICodeZeroActionResultWrapper CreateFor([NotNull] ResultExecutingContext actionResult);
    }
}