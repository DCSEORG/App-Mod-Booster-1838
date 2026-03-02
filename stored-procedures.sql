-- stored-procedures.sql
-- Stored procedures for the Expense Management System
-- All app code uses these stored procedures - no direct table access

-- =============================================
-- usp_GetExpenses
-- Returns expenses with optional filters
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenses
    @StatusId   INT  = NULL,
    @UserId     INT  = NULL,
    @DateFrom   DATE = NULL,
    @DateTo     DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email        AS UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountDecimal,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rv.UserName   AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users           u  ON e.UserId     = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus   s  ON e.StatusId   = s.StatusId
    LEFT JOIN dbo.Users      rv ON e.ReviewedBy = rv.UserId
    WHERE
        (@StatusId IS NULL OR e.StatusId = @StatusId)
        AND (@UserId IS NULL OR e.UserId = @UserId)
        AND (@DateFrom IS NULL OR e.ExpenseDate >= @DateFrom)
        AND (@DateTo   IS NULL OR e.ExpenseDate <= @DateTo)
    ORDER BY e.CreatedAt DESC;
END;
GO

-- =============================================
-- usp_GetExpenseById
-- Returns a single expense by ID
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email        AS UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountDecimal,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rv.UserName   AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users           u  ON e.UserId     = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus   s  ON e.StatusId   = s.StatusId
    LEFT JOIN dbo.Users      rv ON e.ReviewedBy = rv.UserId
    WHERE e.ExpenseId = @ExpenseId;
END;
GO

-- =============================================
-- usp_CreateExpense
-- Creates a new expense in Draft status
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateExpense
    @UserId      INT,
    @CategoryId  INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3)   = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DraftStatusId INT;
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';

    INSERT INTO dbo.Expenses
        (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, CreatedAt)
    VALUES
        (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS NewExpenseId;
END;
GO

-- =============================================
-- usp_UpdateExpense
-- Updates an expense (only allowed when in Draft status)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_UpdateExpense
    @ExpenseId   INT,
    @CategoryId  INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3)   = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Expenses
    SET
        CategoryId  = @CategoryId,
        AmountMinor = @AmountMinor,
        Currency    = @Currency,
        ExpenseDate = @ExpenseDate,
        Description = @Description,
        ReceiptFile = @ReceiptFile
    WHERE ExpenseId = @ExpenseId;

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- =============================================
-- usp_SubmitExpense
-- Transitions expense from Draft to Submitted
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_SubmitExpense
    @ExpenseId   INT,
    @SubmittedAt DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SubmittedStatusId INT;
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';

    IF @SubmittedAt IS NULL
        SET @SubmittedAt = SYSUTCDATETIME();

    UPDATE dbo.Expenses
    SET
        StatusId    = @SubmittedStatusId,
        SubmittedAt = @SubmittedAt
    WHERE ExpenseId = @ExpenseId;

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- =============================================
-- usp_ApproveExpense
-- Transitions expense from Submitted to Approved
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_ApproveExpense
    @ExpenseId   INT,
    @ReviewedBy  INT,
    @ReviewedAt  DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ApprovedStatusId INT;
    SELECT @ApprovedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';

    IF @ReviewedAt IS NULL
        SET @ReviewedAt = SYSUTCDATETIME();

    UPDATE dbo.Expenses
    SET
        StatusId   = @ApprovedStatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = @ReviewedAt
    WHERE ExpenseId = @ExpenseId;

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- =============================================
-- usp_RejectExpense
-- Transitions expense from Submitted to Rejected
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_RejectExpense
    @ExpenseId  INT,
    @ReviewedBy INT,
    @ReviewedAt DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RejectedStatusId INT;
    SELECT @RejectedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected';

    IF @ReviewedAt IS NULL
        SET @ReviewedAt = SYSUTCDATETIME();

    UPDATE dbo.Expenses
    SET
        StatusId   = @RejectedStatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = @ReviewedAt
    WHERE ExpenseId = @ExpenseId;

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- =============================================
-- usp_GetUsers
-- Returns all active users
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetUsers
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users m ON u.ManagerId = m.UserId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END;
GO

-- =============================================
-- usp_GetCategories
-- Returns all active expense categories
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetCategories
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CategoryId,
        CategoryName,
        IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END;
GO

-- =============================================
-- usp_GetStatuses
-- Returns all expense statuses
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetStatuses
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        StatusId,
        StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END;
GO
