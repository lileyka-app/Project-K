-- ==============================================================================
-- Title: Data Scrubbing Script (Level 2 Anonymization)
-- Description: Anonymizes PII data (Personally Identifiable Information) in a 
--              restored copy of the Production database for use in Dev/Staging.
-- Warning: DO NOT RUN THIS SCRIPT ON PRODUCTION!
-- ==============================================================================

SET NOCOUNT ON;

DECLARE @MailDomain VARCHAR(50) = '@project-k.local';
DECLARE @BlankPhone VARCHAR(20) = '000-000-0000';

PRINT 'Starting data anonymization for non-production environments...';

BEGIN TRANSACTION;
BEGIN TRY

    -- 1. Anonymize AppUsers (AspNetUsers)
    PRINT 'Scrubbing AspNetUsers...';
    UPDATE [AspNetUsers]
    SET 
        [FirstName] = 'User',
        [LastName] = CONVERT(VARCHAR(50), [Id]),
        [Email] = 'test+' + CONVERT(VARCHAR(50), [Id]) + @MailDomain,
        [NormalizedEmail] = UPPER('TEST+' + CONVERT(VARCHAR(50), [Id]) + @MailDomain),
        [UserName] = 'test+' + CONVERT(VARCHAR(50), [Id]) + @MailDomain,
        [NormalizedUserName] = UPPER('TEST+' + CONVERT(VARCHAR(50), [Id]) + @MailDomain),
        [PhoneNumber] = @BlankPhone,
        [PasswordHash] = 'AQAAAAIAAYagAAAAEIe2/WbWfI/o/5gD5sK7/5O4nI55PZ5PzG1I2g3z3+2mUq2E2xH/W==', -- Some default dev password hash if needed
        [SecurityStamp] = NEWID(),
        [ConcurrencyStamp] = NEWID();

    -- 2. Anonymize Members
    PRINT 'Scrubbing Members...';
    UPDATE [Members]
    SET
        [FirstName] = 'Member',
        [LastName] = CONVERT(VARCHAR(50), [MemberKey]),
        [MiddleName] = '',
        [Email] = 'member+' + CONVERT(VARCHAR(50), [MemberKey]) + @MailDomain,
        [PhoneNumber] = @BlankPhone,
        [Address] = 'Anonymized Address',
        [School] = 'Anonymized School',
        [DateOfBirth] = DATEADD(DAY, (ABS(CHECKSUM(NEWID())) % 365) * -1, '2000-01-01');

    -- 3. Anonymize Waitlist Entries
    PRINT 'Scrubbing WaitlistEntries...';
    UPDATE [WaitlistEntries]
    SET
        [FirstName] = 'Waitlist',
        [LastName] = CONVERT(VARCHAR(50), [WaitlistEntryKey]),
        [Email] = 'waitlist+' + CONVERT(VARCHAR(50), [WaitlistEntryKey]) + @MailDomain,
        [PhoneNumber] = @BlankPhone,
        [DateOfBirth] = DATEADD(DAY, (ABS(CHECKSUM(NEWID())) % 365) * -1, '2000-01-01');

    COMMIT TRANSACTION;
    PRINT 'Data anonymization completed successfully.';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred during anonymization:';
    PRINT ERROR_MESSAGE();
    THROW;
END CATCH;
