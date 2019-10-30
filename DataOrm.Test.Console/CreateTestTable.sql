CREATE DATABASE test
	CHARACTER SET utf8mb4
	COLLATE utf8mb4_0900_ai_ci;

CREATE TABLE test.entitynormalizedvalues (
  EntityNormalizedValueId BIGINT(20) NOT NULL AUTO_INCREMENT,
  TransactionId BIGINT(20) NOT NULL,
  BrandId VARCHAR(10) DEFAULT NULL,
  BrandName VARCHAR(100) DEFAULT NULL,
  EntityName VARCHAR(300) DEFAULT NULL,
  EntityNameSpecific VARCHAR(300) DEFAULT NULL,
  StreetName VARCHAR(500) DEFAULT NULL,
  StreetNumber VARCHAR(100) DEFAULT NULL,
  StreetType VARCHAR(100) DEFAULT NULL,
  DirectionalPrefix VARCHAR(50) DEFAULT NULL,
  ExtensionName VARCHAR(50) DEFAULT NULL,
  ExtensionNumber VARCHAR(50) DEFAULT NULL,
  PostalCode VARCHAR(30) DEFAULT NULL,
  CityName VARCHAR(50) DEFAULT NULL,
  CountryCode VARCHAR(10) DEFAULT NULL,
  CountryName VARCHAR(100) DEFAULT NULL,
  AirportCityCode CHAR(3) DEFAULT NULL,
  PhoneNumber VARCHAR(100) DEFAULT NULL,
  FaxNumber VARCHAR(100) DEFAULT NULL,
  NoiseWords VARCHAR(400) DEFAULT NULL,
  AttributeValues VARCHAR(400) DEFAULT NULL,
  BrandIdFieldSourceId TINYINT(4) DEFAULT NULL,
  BrandNameFieldSourceId TINYINT(4) DEFAULT NULL,
  StreetNameFieldSourceId TINYINT(4) DEFAULT NULL,
  StreetNumberFieldSourceId TINYINT(4) DEFAULT NULL,
  StreetTypeFieldSourceId TINYINT(4) DEFAULT NULL,
  PostalCodeFieldSourceId TINYINT(4) DEFAULT NULL,
  CityNameFieldSourceId TINYINT(4) DEFAULT NULL,
  CountryCodeFieldSourceId TINYINT(4) DEFAULT NULL,
  AirportCityCodeFieldSourceId TINYINT(4) DEFAULT NULL,
  PhoneNumberFieldSourceId TINYINT(4) DEFAULT NULL,
  FaxNumberFieldSourceId TINYINT(4) DEFAULT NULL,
  CreatedTimeUtc TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  OriginalCountryCode VARCHAR(2) DEFAULT NULL,
  QualityLevelId TINYINT(4) NOT NULL,
  PRIMARY KEY (EntityNormalizedValueId)
)
ENGINE = INNODB,
AUTO_INCREMENT = 0,
AVG_ROW_LENGTH = 184,
CHARACTER SET utf8,
COLLATE utf8_general_ci;

ALTER TABLE test.entitynormalizedvalues 
  ADD INDEX IX_EntityNormalizedValues_TransactionId(TransactionId);
