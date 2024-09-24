using Server.Hubs.Records;

namespace Server.Models;
public class Company(string name, int playerId)
{
    public int? Id { get; private set; }
    public string Name { get; set; } = name;
    public int PlayerId { get; set; } = playerId;
    public Player Player { get; set; } = null!;
    public int Treasury { get; set; } = 1000000;
    public ICollection<Employee> Employees { get; } = [];


    public void DeductSalaries()
    {
        foreach (var employee in Employees)
        {
            int dailySalary = employee.Salary / 365;

            if (Treasury >= dailySalary)
            {
                Treasury -= dailySalary;
                Console.WriteLine($"Deducted {dailySalary} from treasury for employee {employee.Name}. Remaining treasury: {Treasury}");
            }
            else
            {
                Console.WriteLine($"Error: Not enough funds in treasury to pay {employee.Name}.");
            }
        }
    }


    // Convertir en aperçu (overview)
    public CompanyOverview ToOverview()
    {
        return new CompanyOverview(
            Id is null ? 0 : (int) Id, Name,
            Treasury, Employees.Select(e => e.ToOverview()).ToList()
        );
    }
}