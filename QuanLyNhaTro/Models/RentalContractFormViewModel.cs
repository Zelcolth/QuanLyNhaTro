namespace QuanLyNhaTro.Models;

public class RentalContractFormViewModel
{
    public int ContractId { get; set; }

    public int RoomId { get; set; }

    public string ContractCode { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal MonthlyRent { get; set; }

    public decimal Deposit { get; set; }

    public string Status { get; set; } = "Draft";

    public string? Terms { get; set; }

    public int? PrimaryTenantId { get; set; }

    public List<ContractTenantOptionInput> TenantOptions { get; set; } = new();
}

public class ContractTenantOptionInput
{
    public int TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public bool IsSelected { get; set; }

    public string? RelationshipToPrimaryTenant { get; set; }
}
