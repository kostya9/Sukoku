using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sukoku.Pages;

public class Index : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Sudoku.Complexity Complexity { get; set; } = Sudoku.Complexity.Medium;
    
    public void OnGet()
    {
        
    }
}