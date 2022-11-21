//----------------------
// <auto-generated>
//     Generated using the NJsonSchema v10.1.21.0 (Newtonsoft.Json v12.0.0.0) (http://NJsonSchema.org)
// </auto-generated>
//----------------------
using Elements;
using Elements.GeoJSON;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Elements.Spatial;
using Elements.Validators;
using Elements.Serialization.JSON;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Line = Elements.Geometry.Line;
using Polygon = Elements.Geometry.Polygon;

namespace Elements
{
    #pragma warning disable // Disable all warnings

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v12.0.0.0)")]
    public enum SystemVisibilityState
    {
        [System.Runtime.Serialization.EnumMember(Value = @"hidden")]
        Hidden = 0,
    
        [System.Runtime.Serialization.EnumMember(Value = @"visible")]
        Visible = 1,
    
        [System.Runtime.Serialization.EnumMember(Value = @"context")]
        Context = 2,
    
        [System.Runtime.Serialization.EnumMember(Value = @"isolated")]
        Isolated = 3,
    
        [System.Runtime.Serialization.EnumMember(Value = @"isolatedInContext")]
        IsolatedInContext = 4,
    
    }
}