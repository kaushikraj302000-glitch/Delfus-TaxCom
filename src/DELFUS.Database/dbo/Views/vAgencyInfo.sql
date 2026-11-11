




CREATE VIEW [dbo].[vAgencyInfo]
AS

SELECT Id, BatchId, RecordStatus, DisqualifyReason, PdfFileName, LTRIM(RTRIM(AgencyNumber)) AS AgencyNumber, LTRIM(RTRIM(AgencyName)) AS AgencyName, LTRIM(RTRIM(TaxCollectorName)) AS TaxCollectorName, LTRIM(RTRIM(TaxCollectorAddress)) AS TaxCollectorAddress, LTRIM(RTRIM(TaxCollectorCity)) AS TaxCollectorCity, LTRIM(RTRIM(TaxCollectorState)) AS TaxCollectorState, LTRIM(RTRIM(TaxCollectorZip)) AS TaxCollectorZip, LTRIM(RTRIM(ELD)) AS ELD, LTRIM(RTRIM(DueDate)) AS DueDate
FROM tpv.AgencyInfo