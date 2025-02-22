﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Microsoft.AspNetCore.Mvc.Analyzers.Test
{
    [Authorize]
    public class NoDiagnosticsAreReturned_IfAuthorizeAttributeIsAppliedToPageModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
