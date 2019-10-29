using System;

public partial class EntityNormalizedValue
{
    public long EntityNormalizedValueId { get; set; }
    public long TransactionId { get; set; }
    public string BrandId { get; set; }
    public string BrandName { get; set; }
    public string EntityName { get; set; }
    public string EntityNameSpecific { get; set; }
    public string StreetName { get; set; }
    public string StreetNumber { get; set; }
    public string StreetType { get; set; }
    public string DirectionalPrefix { get; set; }
    public string ExtensionName { get; set; }
    public string ExtensionNumber { get; set; }
    public string PostalCode { get; set; }
    public string CityName { get; set; }
    public string CountryCode { get; set; }
    public string CountryName { get; set; }
    public string AirportCityCode { get; set; }
    public string PhoneNumber { get; set; }
    public string FaxNumber { get; set; }
    public string NoiseWords { get; set; }
    public string AttributeValues { get; set; }
    public byte? BrandIdFieldSourceId { get; set; }
    public byte? BrandNameFieldSourceId { get; set; }
    public byte? StreetNameFieldSourceId { get; set; }
    public byte? StreetNumberFieldSourceId { get; set; }
    public byte? StreetTypeFieldSourceId { get; set; }
    public byte? PostalCodeFieldSourceId { get; set; }
    public byte? CityNameFieldSourceId { get; set; }
    public byte? CountryCodeFieldSourceId { get; set; }
    public byte? AirportCityCodeFieldSourceId { get; set; }
    public byte? PhoneNumberFieldSourceId { get; set; }
    public byte? FaxNumberFieldSourceId { get; set; }
    public DateTime CreatedTimeUtc { get; set; }
    public string OriginalCountryCode { get; set; }
    public byte QualityLevelId { get; set; }
    public int QualityLevel { get; set; }
}
