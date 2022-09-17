using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Document = QuestPDF.Fluent.Document;

Document GeneratePdf(int[,] ints)
{
    var document1 = Document.Create(x =>
    {
        x.Page(p =>
        {
            p.Margin(1, Unit.Centimetre);
            p.Size(PageSizes.A4);
            p.Content()
                .AlignMiddle()
                .Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        for (int i = 0; i < Sudoku.SudokuSideSize; i++)
                        {
                            cd.RelativeColumn();
                        }
                    });

                    for (int i = 0; i < Sudoku.SudokuSideSize; i++)
                    {
                        for (int j = 0; j < Sudoku.SudokuSideSize; j++)
                        {
                            
                            var cellSize = 55;
                            var fontSize = 45;
                            var value = ints[i, j];
                            var c = t.Cell().MinHeight(cellSize).MinWidth(cellSize).Background(Colors.Grey.Lighten3);

                            var thickBorder = 2.5f;
                            var thinBorder = 0.5f;

                            var topBorder = i % Sudoku.SquareSideSize == 0 ? thickBorder : thinBorder;
                            var bottomBorder = i % Sudoku.SquareSideSize == (Sudoku.SquareSideSize - 1) ? thickBorder : thinBorder;
                            var leftBorder = j % Sudoku.SquareSideSize == 0 ? thickBorder : thinBorder;
                            var rightBorder = j % Sudoku.SquareSideSize == (Sudoku.SquareSideSize - 1) ? thickBorder : thinBorder;

                            c = c.BorderTop(topBorder)
                                .BorderBottom(bottomBorder)
                                .BorderLeft(leftBorder)
                                .BorderRight(rightBorder);
                            
                            c = c.ShowOnce().AlignCenter().AlignMiddle();
                            if (value != Sudoku.EmptyCellValue)
                            {
                                c.Text(td =>
                                {
                                    td.AlignCenter();
                                    td.Element().Text(value).FontSize(fontSize).ExtraBold();
                                });
                            }
                        }
                    }
                });
        });
    });
    return document1;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

app.MapGet("/hehe", ([FromQuery] int seed) => JsonConvert.SerializeObject(Sudoku.Generate(seed)));

app.MapGet("/sudoku/{complexity}/pdf/{seed}", (Sudoku.Complexity complexity, int seed) =>
{
    var sudoku = Sudoku.Generate(seed);
    var withHidden = sudoku.HideForComplexity(complexity);

    var document = GeneratePdf(withHidden);

    return Results.File(document.GeneratePdf(), "application/pdf", $"sudoku_{complexity}_{seed}.pdf");
});

app.MapGet("/sudoku/{complexity}/pdf/{seed}/preview", (Sudoku.Complexity complexity, int seed) =>
{
    var sudoku = Sudoku.Generate(seed);
    var withHidden = sudoku.HideForComplexity(complexity);

    var document = GeneratePdf(withHidden);

    return Results.File(document.GeneratePdf(), "application/pdf");
});

app.MapGet("/sudoku/{complexity}/pdf/{seed}/preview/solved", (Sudoku.Complexity complexity, int seed) =>
{
    var sudoku = Sudoku.Generate(seed);

    var document = GeneratePdf(sudoku.Values);

    return Results.File(document.GeneratePdf(), "application/pdf");
});

app.MapRazorPages();

app.Run();

public class Sudoku
{
    private readonly int _seed;

    public enum Complexity
    {
        Easy,
        Medium,
        Hard
    }
    
    public const int SquareSideSize = 3;
    public const int SudokuSideSize = SquareSideSize * SquareSideSize;
    public const int EmptyCellValue = 0;
    
    public Sudoku(int[,] values, int seed)
    {
        if (values.GetLength(0) != SudokuSideSize || values.GetLength(1) != SudokuSideSize)
            throw new ArgumentException(nameof(values));
        _seed = seed;

        Values = values;
    }

    public int[,] Values { get; }

    public int[,] HideForComplexity(Complexity complexity)
    {
        var random = new Random(_seed);
        var valuesWithHidden = new int[SudokuSideSize, SudokuSideSize];
        
        var revealedNumbers = complexity switch
        {
            Complexity.Easy => random.Next(36, 50),
            Complexity.Medium => random.Next(27, 36),
            Complexity.Hard => random.Next(19, 27),
            _ => throw new ArgumentOutOfRangeException(nameof(complexity), complexity, null)
        };

        // Reveal numbers in all squares
        var squaresAmount = SudokuSideSize;
        var revealedPerSquareNumber = revealedNumbers / squaresAmount;
        var squaresWithExtraRevealed = revealedNumbers % squaresAmount;

        Dictionary<int, int> revealedPerSquare = new Dictionary<int, int>();
        var squares = Enumerable.Range(0, squaresAmount).OrderBy(x => random.Next()).ToArray();

        for (int i = 0; i < squaresAmount; i++)
        {
            var numberOfRevealed = revealedPerSquareNumber;

            if (squaresWithExtraRevealed > 0)
            {
                numberOfRevealed++;
                squaresWithExtraRevealed--;
            }
            
            revealedPerSquare[squares[i]] = numberOfRevealed;
        }

        foreach (var (square, revealedNumber) in revealedPerSquare)
        {
            var revealedIndices = Enumerable.Range(0, SudokuSideSize).OrderBy(x => random.Next()).Take(revealedNumber).ToArray();

            foreach (var revealedIndex in revealedIndices)
            {
                var sqRow = revealedIndex / SquareSideSize;
                var sqCol = revealedIndex % SquareSideSize;

                var sqStartRow = SquareSideSize * (square / SquareSideSize);
                var sqStartCol = SquareSideSize * (square % SquareSideSize);
                    
                var row = sqStartRow + sqRow;
                var col = sqStartCol + sqCol;

                valuesWithHidden[row, col] = Values[row, col];   
            }
        }

        return valuesWithHidden;
    }

    public static Sudoku Generate(int seed)
    {
        var random = new Random(seed);
        var values = new int[SudokuSideSize, SudokuSideSize];

        while (true)
        {
            if (TryGenerateSudoku(random, values))
            {
                return new Sudoku(values, seed);
            }
        }
    }
    
    private static bool TryGenerateSudoku(Random random, int[,] values)
    {
        for (int rowIdx = 0; rowIdx < SudokuSideSize; rowIdx++)
        {
            var row = Enumerable.Range(1, SudokuSideSize).OrderBy(_ => random.Next()).ToArray();

            for (int colIdx = 0; colIdx < SudokuSideSize; colIdx++)
            {
                var swapCandidateIdx = colIdx + 1;
                var candidate = row[colIdx];

                while (HasSameNumberAbove(values, rowIdx, colIdx, candidate) || HasSameNumberInSquare(values, rowIdx, colIdx, candidate))
                {
                    if (swapCandidateIdx >= SudokuSideSize)
                    {
                        // Failed
                        return false;
                    }

                    var swapCandidate = row[swapCandidateIdx];
                    row[swapCandidateIdx] = candidate;
                    row[colIdx] = swapCandidate;
                    candidate = swapCandidate;

                    swapCandidateIdx++;
                }

                values[rowIdx, colIdx] = candidate;
            }
        }

        return true;
    }

    private static bool HasSameNumberInSquare(int[,] values, int rowIdx, int colIdx, int candidate)
    {
        var squareRowIdx = rowIdx / SquareSideSize;
        var squareColIdx = colIdx / SquareSideSize;

        for (var i = squareRowIdx * SquareSideSize; i < squareRowIdx * SquareSideSize + SquareSideSize; i++)
        {
            for (var j = squareColIdx * SquareSideSize; j < squareColIdx * SquareSideSize + SquareSideSize; j++)
            {
                if(i == rowIdx && j == colIdx)
                    continue;

                if (values[i, j] == candidate)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasSameNumberAbove(int[,] values, int row, int column, int candidate)
    {
        for (int i = row - 1; i >= 0; i--)
        {
            if (values[i, column] == candidate)
            {
                return true;
            }
        }

        return false;
    }
}