﻿namespace DataMasker
{
  /// <summary>
  /// 
  /// </summary>
  public enum DataType
  {
    /// <summary>
    /// The none
    /// </summary>
    None,

    /// <summary>
    /// The api data type, supports {{entity.property}} e.g. {{address.FullAddress}}
    /// </summary>
    Bogus,

    /// <summary>
    /// The first name
    /// </summary>
    FirstName,

    /// <summary>
    /// The last name
    /// </summary>
    LastName,

    /// <summary>
    /// The date of birth
    /// </summary>
    DateOfBirth,

    /// <summary>
    /// The rant
    /// </summary>
    Rant,

    /// <summary>
    /// The lorem
    /// </summary>
    Lorem,

    /// <summary>
    /// The string format
    /// </summary>
    StringFormat,

    /// <summary>
    /// The full address
    /// </summary>
    FullAddress,

    /// <summary>
    /// The phone number
    /// </summary>
    PhoneNumber,

    /// <summary>
    /// Indicates that the column value is computed from other columns
    /// </summary>
    Computed,
    /// <summary>
    /// The value for this column should come from a scalar Query
    /// </summary>
    Sql
  }
}
