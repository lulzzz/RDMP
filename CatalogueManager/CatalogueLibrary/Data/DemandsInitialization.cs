﻿using System;

namespace CatalogueLibrary.Data
{

    /// <summary>
    /// Used by classes to indicate that a property should be initialized from a ProcessTaskArgument
    /// </summary>
    [System.AttributeUsage(AttributeTargets.Property)]
    public class DemandsInitialization:System.Attribute
    {
        public string Description { get; private set; }
        public DemandType DemandType { get; set; }
        public object DefaultValue { get; set; }
        public bool Mandatory { get; set; }

        public Type TypeOf { get; private set; }

        public DemandsInitialization(string description,DemandType demandType =  DemandType.Unspecified, object defaultValue = null, Type typeOf = null, bool mandatory = false)
        {
            Description = description;
            DemandType = demandType;
            DefaultValue = defaultValue;
            TypeOf = typeOf;
            Mandatory = mandatory;

        }
    }

    public enum DemandType
    {
        Unspecified,
        SQL
    }
}
