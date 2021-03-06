﻿using System;
using System.Linq.Expressions;

namespace CerebelloWebRole.Code
{
    public class EditPanelTextField<TModel, TValue> : EditPanelFieldBase
    {
// ReSharper disable MemberCanBePrivate.Global
        // this member is accessed via reflaction and being public makes it easier
        public Expression<Func<TModel, TValue>> Expression { get; set; }
// ReSharper restore MemberCanBePrivate.Global

        public EditPanelTextField(Expression<Func<TModel, TValue>> exp, Func<dynamic, object> format = null, string header = null, bool foreverAlone = false)
        {
            this.Format = format;
            this.Expression = exp;
            this.Header = header;
            this.WholeRow = foreverAlone;
        }
    }
}