using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.ML;
using Microsoft.ML.Data;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class MatchController : ControllerBase
{
    public class TextData
    {
        public string Text { get; set; }
    }

    public class TransformedTextData : TextData
    {
        [VectorType]
        public float[] Features { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm] IFormFile resume, [FromForm] IFormFile jd)
    {
        string resumeText = ExtractText(resume);
        string jdText = ExtractText(jd);

        var mlContext = new MLContext();

        var data = mlContext.Data.LoadFromEnumerable(new List<TextData>
        {
            new TextData { Text = resumeText },
            new TextData { Text = jdText }
        });

        var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(TextData.Text));
        var model = pipeline.Fit(data);
        var transformedData = model.Transform(data);

        var features = mlContext.Data.CreateEnumerable<TransformedTextData>(transformedData, reuseRowObject: false).ToList();

        var dot = features[0].Features.Zip(features[1].Features, (a, b) => a * b).Sum();
        var mag1 = Math.Sqrt(features[0].Features.Sum(x => x * x));
        var mag2 = Math.Sqrt(features[1].Features.Sum(x => x * x));
        var cosineSimilarity = dot / (mag1 * mag2);

        return Ok(new
        {
            score = Math.Round(cosineSimilarity, 2),
            match = cosineSimilarity > 0.6
        });
    }

    private string ExtractText(IFormFile file)
    {
        using var stream = new MemoryStream();
        file.CopyTo(stream);
        stream.Position = 0;

        var sb = new StringBuilder();
        var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.ReadOnly);

        foreach (var page in pdf.Pages)
        {
            // Simple workaround for PdfSharpCore limitations
            var text = page.Contents.CreateSingleContent().Stream.ToString();
            sb.AppendLine(text);
        }

        return sb.ToString();
    }
}
