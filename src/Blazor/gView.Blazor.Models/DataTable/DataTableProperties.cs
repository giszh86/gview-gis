﻿using gView.Framework.Core.Data;
using gView.Framework.Core.Data.Filters;
using System.Collections.Generic;

namespace gView.Blazor.Models.DataTable;

public class DataTableProperties
{
    public IEnumerable<IField> TableFields { get; set; } = [];
    public bool CanSelect { get; set; }

    public Dictionary<string, string> ColumnFilters { get; set; } = new();
    public bool HasMore { get; set; }
    public string FilterWhereClause { get; set; } = "";
    public string OrderByClause { get; set; } = "";
    public string SearchString { get; set; } = "";

    public ISpatialFilter? IdentifyFilter { get; set; }

    public Mode DataMode { get; set; } = Mode.AllData;
}
