using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIChess.Core.Models;

public static class EnumHelpers
{
    public static string GetDescription(this Enum value)
    {
        var type = value.GetType();
        var name = Enum.GetName(type, value);
        if (name == null)
        {
            return null;
        }
        var field = type.GetField(name);
        var attr = Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) as System.ComponentModel.DescriptionAttribute;
        return attr?.Description;
    }
    public static string GetProvider(this Enum value)
    {
        var type = value.GetType();
        var name = Enum.GetName(type, value);
        if (name == null)
        {
            return null;
        }
        var field = type.GetField(name);
        var attr = Attribute.GetCustomAttribute(field, typeof(ProviderAttribute)) as ProviderAttribute;
        return attr?.Name;
    }
}