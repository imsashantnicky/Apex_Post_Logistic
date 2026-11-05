/*
    Paid sales of one branch on one UTC day, split into retail and services: the signature the brief asks for.

    A thin wrapper over usp_DailyBranchSalesRange, so one body computes every sales figure and the measured plan stays
    the plan that runs. The range procedure validates the branch and sniffs the real day, because the day reaches it
    as a parameter, not as a local variable. @BranchCode NULL means all branches.

    Errors: 51004 no date, and those of usp_DailyBranchSalesRange (51003 unknown branch).
*/
CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
    @BranchCode varchar(5),
    @Date date
AS
BEGIN
    SET NOCOUNT ON;

    IF @Date IS NULL
        THROW 51004, 'usp_DailyBranchSales: @Date is required.', 1;

    EXEC dbo.usp_DailyBranchSalesRange @From = @Date, @To = @Date, @BranchCode = @BranchCode;
END;
