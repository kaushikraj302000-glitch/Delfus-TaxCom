



CREATE VIEW [dbo].[vAgencyPropertyInfo]
AS
SELECT Id, BatchId, AgencyId, LTRIM(RTRIM(AgencyNumber)) AS AgencyNumber, LTRIM(RTRIM(AgencyName)) AS AgencyName, LTRIM(RTRIM(ParcelNumber)) AS ParcelNumber, LTRIM(RTRIM(PropertyName)) AS PropertyName, LTRIM(RTRIM(PropertyStreetAddress)) AS PropertyStreetAddress, LTRIM(RTRIM(KeyNo)) AS KeyNo
FROM tpv.AgencyPropertyInfo