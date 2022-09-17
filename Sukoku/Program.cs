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
                        for (int i = 0; i < Sudoku.SudokuLength; i++)
                        {
                            cd.RelativeColumn();
                        }
                    });

                    for (int i = 0; i < Sudoku.SudokuLength; i++)
                    {
                        for (int j = 0; j < Sudoku.SudokuLength; j++)
                        {
                            
                            var cellSize = 55;
                            var fontSize = 45;
                            var value = ints[i, j];
                            var c = t.Cell().MinHeight(cellSize).MinWidth(cellSize).Background(Colors.Grey.Lighten3);

                            var thickBorder = 2;
                            var thinBorder = 0.5f;

                            var topBorder = i % 3 == 0 ? thickBorder : thinBorder;
                            var bottomBorder = i % 3 == 2 ? thickBorder : thinBorder;
                            var leftBorder = j % 3 == 0 ? thickBorder : thinBorder;
                            var rightBorder = j % 3 == 2 ? thickBorder : thinBorder;

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
                                    td.Element().Text(value).FontSize(fontSize);
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
    
    public const int SudokuLength = 9;
    public const int SquareSize = 3;
    public const int EmptyCellValue = 0;
    
    public Sudoku(int[,] values, int seed)
    {
        if (values.GetLength(0) != SudokuLength || values.GetLength(1) != SudokuLength)
            throw new ArgumentException(nameof(values));
        _seed = seed;

        Values = values;
    }

    public int[,] Values { get; }

    public int[,] HideForComplexity(Complexity complexity)
    {
        var random = new Random(_seed);
        var valuesWithHidden = new int[SudokuLength, SudokuLength];
        
        var revealedNumbers = complexity switch
        {
            Complexity.Easy => random.Next(36, 50),
            Complexity.Medium => random.Next(27, 36),
            Complexity.Hard => random.Next(19, 27),
            _ => throw new ArgumentOutOfRangeException(nameof(complexity), complexity, null)
        };

        // Reveal numbers in all squares
        var squaresAmount = SudokuLength;
        var revealedPerSquareNumber = revealedNumbers / squaresAmount;
        var squaresWithExtraRevealed = revealedNumbers % squaresAmount;

        Dictionary<int, int> revealedPerSquare = new Dictionary<int, int>();
        var squares = Enumerable.Range(0, squaresAmount).OrderBy(x => Random.Shared.Next()).ToArray();

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
            var revealedIndices = Enumerable.Range(0, SudokuLength).OrderBy(x => Random.Shared.Next()).Take(revealedNumber).ToArray();

            foreach (var revealedIndex in revealedIndices)
            {
                var sqRow = revealedIndex / SquareSize;
                var sqCol = revealedIndex % SquareSize;

                var sqStartRow = SquareSize * (square / SquareSize);
                var sqStartCol = SquareSize * (square % SquareSize);
                    
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
        var values = new int[SudokuLength, SudokuLength];

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
        for (int rowIdx = 0; rowIdx < SudokuLength; rowIdx++)
        {
            var row = Enumerable.Range(1, SudokuLength).OrderBy(_ => random.Next()).ToArray();

            for (int colIdx = 0; colIdx < SudokuLength; colIdx++)
            {
                var swapCandidateIdx = colIdx + 1;
                var candidate = row[colIdx];

                while (HasNumberHigher(values, rowIdx, colIdx, candidate) || HasNumberInSquare(values, rowIdx, colIdx, candidate))
                {
                    if (swapCandidateIdx >= SudokuLength)
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

    private static bool HasNumberInSquare(int[,] values, int rowIdx, int colIdx, int candidate)
    {
        var squareRowIdx = rowIdx / 3;
        var squareColIdx = colIdx / 3;

        for (int i = squareRowIdx * 3; i < squareRowIdx * 3 + 3; i++)
        {
            for (int j = squareColIdx * 3; j < squareColIdx * 3 + 3; j++)
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

    private static bool HasNumberHigher(int[,] values, int row, int column, int candidate)
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