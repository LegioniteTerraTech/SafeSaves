using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using UnityEngine;
using Ionic.Zlib;
using Newtonsoft.Json;


namespace SafeSaves
{
    /// <summary>
    /// Use this Attribute for Fields that must be saved on save
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SSaveFieldAttribute : Attribute
    {
        public SSaveFieldAttribute()
        {
        }
    }

    /// <summary>
    /// Use this Attribute to enable saving of values with AutoSaveField in Block Modules.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoSaveComponentAttribute : Attribute
    {
        public AutoSaveComponentAttribute()
        {
        }
    }

    /// <summary>
    /// Use this Attribute to enable saving of values with AutoSaveField in a Tank.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoSaveVisibleAttribute : Attribute
    {
        public AutoSaveVisibleAttribute()
        {
        }
    }

    /// <summary>
    /// Attach this Attribute to the single Manager instance
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SSManagerInstAttribute : Attribute
    {
        public SSManagerInstAttribute()
        {
        }
    }

    /// <summary>
    /// Use this Attribute for Managers (Classes that manage others and only have one instance)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoSaveManagerAttribute : Attribute
    {
        public AutoSaveManagerAttribute()
        {
        }
    }
}
