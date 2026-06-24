using System.Xml.Linq;
using BiobankApi.Domain.Patients;
using ErrorOr;

namespace BiobankApi.Infrastructure.Xml;

/// <summary>
/// Maps a single biobank <c>&lt;patient&gt;</c> element to a domain <see cref="PatientAggregate"/>.
/// Pure (no IO) so it can be unit-tested directly from an <see cref="XElement"/>. Raw text is decoded
/// with <see cref="XmlValueReader"/>; invariants are enforced by the aggregates' <c>Create</c> factories.
/// Mapping is <b>whole-patient atomic</b>: any decode or validation error fails the whole patient, so a
/// half-built patient is never produced.
/// </summary>
internal static class XmlPatientReader
{
    private static readonly XNamespace Ns = "http://www.bbmri.cz/schemas/biobank/data";

    public static ErrorOr<PatientAggregate> Read(XElement patient)
    {
        var fields = new FieldErrors();

        var patientId = XmlValueReader.CleanText(Attr(patient, "id"));
        var biobank = XmlValueReader.CleanText(Attr(patient, "biobank"));
        var consent = fields.Take(XmlValueReader.ParseConsent(Attr(patient, "consent")));
        var sex = fields.Take(XmlValueReader.ParseSex(Attr(patient, "sex")));
        var birthYear = fields.Take(XmlValueReader.ParseYear(Attr(patient, "year")));
        var birthMonth = fields.Take(XmlValueReader.ParseMonth(Attr(patient, "month")));
        var accessionNumbers = ReadAccessions(patient);

        var samples = new List<Sample>();
        if (patient.Element(Ns + "LTS") is { } lts)
        {
            foreach (var element in lts.Elements(Ns + "tissue"))
            {
                fields.Collect(ReadTissue(element), samples);
            }

            foreach (var element in lts.Elements(Ns + "serum"))
            {
                fields.Collect(ReadSerum(element), samples);
            }

            foreach (var element in lts.Elements(Ns + "genome"))
            {
                fields.Collect(ReadGenome(element), samples);
            }
        }

        // diagnosisMaterial appears under STS (and occasionally LTS); gather it wherever it sits.
        var specimens = new List<DiagnosticSpecimen>();
        foreach (var element in patient.Descendants(Ns + "diagnosisMaterial"))
        {
            fields.Collect(ReadDiagnosticSpecimen(element), specimens);
        }

        if (fields.HasErrors)
        {
            return fields.Errors;
        }

        return PatientAggregate.Create(
            patientId ?? string.Empty,
            biobank,
            consent,
            sex,
            birthYear,
            birthMonth,
            accessionNumbers,
            samples,
            specimens);
    }

    private static ErrorOr<TissueSample> ReadTissue(XElement element)
    {
        var fields = new FieldErrors();
        var sampleId = XmlValueReader.CleanSampleId(Attr(element, "sampleId"));
        var materialType = XmlValueReader.CleanText(Elem(element, "materialType"));
        var eventNumber = fields.Take(XmlValueReader.ParseInt(Attr(element, "number")));
        var collectionYear = fields.Take(XmlValueReader.ParseYear(Attr(element, "year")));
        var biopsy = XmlValueReader.DashToNone(Attr(element, "biopsy"));
        var predictiveNumber = XmlValueReader.DashToNone(Attr(element, "predictive_number"));
        var samplesNo = fields.Take(XmlValueReader.ParseInt(Elem(element, "samplesNo")));
        var availableSamplesNo = fields.Take(XmlValueReader.ParseInt(Elem(element, "availableSamplesNo")));
        var accessionNumbers = ReadAccessions(element);
        var diagnosis = XmlValueReader.CleanCode(Elem(element, "diagnosis"));
        var pTnm = XmlValueReader.CleanCode(Elem(element, "pTNM"));
        var morphology = XmlValueReader.CleanCode(Elem(element, "morphology"));
        var cutTime = fields.Take(XmlValueReader.ParseTemporal(Elem(element, "cutTime")));
        var freezeTime = fields.Take(XmlValueReader.ParseTemporal(Elem(element, "freezeTime")));
        var retrieved = fields.Take(XmlValueReader.ParseRetrieved(Elem(element, "retrieved")));

        if (fields.HasErrors)
        {
            return fields.Errors;
        }

        return TissueSample.Create(
            sampleId ?? string.Empty,
            materialType ?? string.Empty,
            eventNumber,
            collectionYear,
            biopsy,
            predictiveNumber,
            samplesNo,
            availableSamplesNo,
            accessionNumbers,
            diagnosis,
            pTnm,
            morphology,
            cutTime,
            freezeTime,
            retrieved);
    }

    private static ErrorOr<SerumSample> ReadSerum(XElement element)
    {
        var fields = new FieldErrors();
        var sampleId = XmlValueReader.CleanSampleId(Attr(element, "sampleId"));
        var materialType = XmlValueReader.CleanText(Elem(element, "materialType"));
        var eventNumber = fields.Take(XmlValueReader.ParseInt(Attr(element, "number")));
        var collectionYear = fields.Take(XmlValueReader.ParseYear(Attr(element, "year")));
        var biopsy = XmlValueReader.DashToNone(Attr(element, "biopsy"));
        var predictiveNumber = XmlValueReader.DashToNone(Attr(element, "predictive_number"));
        var samplesNo = fields.Take(XmlValueReader.ParseInt(Elem(element, "samplesNo")));
        var availableSamplesNo = fields.Take(XmlValueReader.ParseInt(Elem(element, "availableSamplesNo")));
        var accessionNumbers = ReadAccessions(element);
        var diagnosis = XmlValueReader.CleanCode(Elem(element, "diagnosis"));
        var takingDate = fields.Take(XmlValueReader.ParseTemporal(Elem(element, "takingDate")));
        var retrieved = fields.Take(XmlValueReader.ParseRetrieved(Elem(element, "retrieved")));

        if (fields.HasErrors)
        {
            return fields.Errors;
        }

        return SerumSample.Create(
            sampleId ?? string.Empty,
            materialType ?? string.Empty,
            eventNumber,
            collectionYear,
            biopsy,
            predictiveNumber,
            samplesNo,
            availableSamplesNo,
            accessionNumbers,
            diagnosis,
            takingDate,
            retrieved);
    }

    private static ErrorOr<GenomeSample> ReadGenome(XElement element)
    {
        var fields = new FieldErrors();
        var sampleId = XmlValueReader.CleanSampleId(Attr(element, "sampleId"));
        var materialType = XmlValueReader.CleanText(Elem(element, "materialType"));
        var eventNumber = fields.Take(XmlValueReader.ParseInt(Attr(element, "number")));
        var collectionYear = fields.Take(XmlValueReader.ParseYear(Attr(element, "year")));
        var biopsy = XmlValueReader.DashToNone(Attr(element, "biopsy"));
        var predictiveNumber = XmlValueReader.DashToNone(Attr(element, "predictive_number"));
        var samplesNo = fields.Take(XmlValueReader.ParseInt(Elem(element, "samplesNo")));
        var availableSamplesNo = fields.Take(XmlValueReader.ParseInt(Elem(element, "availableSamplesNo")));
        var accessionNumbers = ReadAccessions(element);
        var takingDate = fields.Take(XmlValueReader.ParseTemporal(Elem(element, "takingDate")));
        var retrieved = fields.Take(XmlValueReader.ParseRetrieved(Elem(element, "retrieved")));

        if (fields.HasErrors)
        {
            return fields.Errors;
        }

        return GenomeSample.Create(
            sampleId ?? string.Empty,
            materialType ?? string.Empty,
            eventNumber,
            collectionYear,
            biopsy,
            predictiveNumber,
            samplesNo,
            availableSamplesNo,
            accessionNumbers,
            takingDate,
            retrieved);
    }

    private static ErrorOr<DiagnosticSpecimen> ReadDiagnosticSpecimen(XElement element)
    {
        var fields = new FieldErrors();
        var sampleId = XmlValueReader.CleanSampleId(Attr(element, "sampleId"));
        var specimenNumber = fields.Take(XmlValueReader.ParseInt(Attr(element, "number")));
        var year = fields.Take(XmlValueReader.ParseYear(Attr(element, "year")));
        var materialType = XmlValueReader.CleanText(Elem(element, "materialType"));
        var diagnosis = XmlValueReader.CleanCode(Elem(element, "diagnosis"));
        var takingDate = fields.Take(XmlValueReader.ParseTemporal(Elem(element, "takingDate")));
        var retrieved = fields.Take(XmlValueReader.ParseRetrieved(Elem(element, "retrieved")));

        if (fields.HasErrors)
        {
            return fields.Errors;
        }

        return DiagnosticSpecimen.Create(
            sampleId ?? string.Empty,
            specimenNumber,
            year,
            materialType,
            diagnosis,
            takingDate,
            retrieved);
    }

    private static IReadOnlyList<string> ReadAccessions(XElement parent)
    {
        if (parent.Element(Ns + "AccessionNumbers") is not { } container)
        {
            return [];
        }

        return XmlValueReader.CleanAccessions(container.Elements(Ns + "Number").Select(number => number.Value));
    }

    private static string? Attr(XElement element, string name) => element.Attribute(name)?.Value;

    private static string? Elem(XElement parent, string name) => parent.Element(Ns + name)?.Value;

    /// <summary>
    /// Accumulates the decode/validation errors for one element so a whole record can fail atomically.
    /// </summary>
    private sealed class FieldErrors
    {
        private readonly List<Error> _errors = [];

        public bool HasErrors => _errors.Count > 0;

        public List<Error> Errors => _errors;

        /// <summary>Unwrap a decoded value, recording any error and yielding the default instead.</summary>
        public T Take<T>(ErrorOr<T> result)
        {
            if (result.IsError)
            {
                _errors.AddRange(result.Errors);
                return default!;
            }

            return result.Value;
        }

        /// <summary>Add a built child to <paramref name="target"/>, or record its errors.</summary>
        public void Collect<TBase, T>(ErrorOr<T> result, List<TBase> target)
            where T : TBase
        {
            if (result.IsError)
            {
                _errors.AddRange(result.Errors);
            }
            else
            {
                target.Add(result.Value);
            }
        }
    }
}
