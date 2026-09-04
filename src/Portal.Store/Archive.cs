namespace Portal.Store;

using System.Reflection;

using Microsoft.Data.Sqlite;

using Portal.Core;

/// <summary>
/// The archive, in SQLite.
/// </summary>
/// <remarks>
/// <para>
/// It implements <see cref="IDocuments"/> and therefore has no way of being
/// asked for a document by identifier alone. That is not enforced here; it is
/// enforced by the interface having no such method, which is the point of
/// putting the rule in a type instead of in a review comment.
/// </para>
/// <para>
/// SQLite because the whole archive in this repository is invented and fits in
/// a file. What a hospital runs behind this is a document store and a PACS, and
/// neither changes the shape of the question.
/// </para>
/// </remarks>
public sealed class Archive : IDocuments, IDisposable
{
    private readonly SqliteConnection database;

    private Archive(SqliteConnection database) => this.database = database;

    /// <summary>Open an archive, creating the tables if they are not there.</summary>
    /// <param name="path">A file, or <c>:memory:</c>.</param>
    /// <returns>The open archive.</returns>
    public static Archive Open(string path = ":memory:")
    {
        var database = new SqliteConnection($"Data Source={path}");
        database.Open();

        using var schema = database.CreateCommand();
        schema.CommandText = Schema();
        schema.ExecuteNonQuery();

        return new Archive(database);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Document>> ListFor(PatientId who, CancellationToken cancel = default)
    {
        await using var query = database.CreateCommand();

        // Every column but the content. A list that carries the bytes is a
        // list that hands out the bytes, and this one is drawn on a page where
        // some of the rows are documents the patient may not open yet.
        query.CommandText =
            """
            SELECT id, belongs, title, released, sensitive
            FROM documents
            WHERE belongs = $who
            ORDER BY id
            """;

        query.Parameters.AddWithValue("$who", who.Value);

        var found = new List<Document>();
        await using var rows = await query.ExecuteReaderAsync(cancel);

        while (await rows.ReadAsync(cancel))
        {
            found.Add(new Document(
                new DocumentId(rows.GetString(0)),
                new PatientId(rows.GetString(1)),
                rows.GetString(2),
                rows.GetInt32(3) == 1,
                rows.GetInt32(4) == 1,
                []));
        }

        return found;
    }

    /// <inheritdoc />
    public async Task<Answer> Answer(Asked question, Confirmed? code = null, CancellationToken cancel = default)
    {
        await using var query = database.CreateCommand();

        // Both terms, in one predicate, in the statement itself. There is no
        // arrangement of this method that fetches by id and then checks the
        // owner, because that is the arrangement that was wrong: the fetch
        // succeeded and the check was somewhere else, or nowhere.
        query.CommandText =
            """
            SELECT id, belongs, title, released, sensitive, content
            FROM documents
            WHERE id = $what AND belongs = $who
            """;

        query.Parameters.AddWithValue("$what", question.For.Value);
        query.Parameters.AddWithValue("$who", question.By.Value);

        await using var rows = await query.ExecuteReaderAsync(cancel);

        if (!await rows.ReadAsync(cancel))
        {
            // Only now, and only for the trail. Asking whether the document
            // exists is a question the patient must never be able to make the
            // portal answer, so it happens after the refusal is already
            // decided and never reaches the response.
            return Core.Answer.No(question, Refusal.NotYours, await Exists(question.For, cancel));
        }

        var document = new Document(
            new DocumentId(rows.GetString(0)),
            new PatientId(rows.GetString(1)),
            rows.GetString(2),
            rows.GetInt32(3) == 1,
            rows.GetInt32(4) == 1,
            (byte[])rows[5]);

        if (!document.Released) return Core.Answer.No(question, Refusal.NotReleasedYet, true);

        // The receipt has to be for this question. A receipt is not a flag
        // saying "the second factor was done" — it names the patient and the
        // document it was done for, and a receipt for another document is
        // worth exactly as much as none.
        if (document.Sensitive && code?.For != question)
        {
            return Core.Answer.No(question, Refusal.NeedsASecondFactor, true);
        }

        return Core.Answer.Handed(question, document);
    }

    /// <summary>Put a document in the archive.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancel">To give up.</param>
    /// <returns>When it is in.</returns>
    public async Task Put(Document document, CancellationToken cancel = default)
    {
        await using var insert = database.CreateCommand();

        insert.CommandText =
            """
            INSERT INTO documents (id, belongs, title, released, sensitive, content)
            VALUES ($id, $belongs, $title, $released, $sensitive, $content)
            ON CONFLICT (id) DO UPDATE SET
              belongs = excluded.belongs,
              title = excluded.title,
              released = excluded.released,
              sensitive = excluded.sensitive,
              content = excluded.content
            """;

        insert.Parameters.AddWithValue("$id", document.Id.Value);
        insert.Parameters.AddWithValue("$belongs", document.Belongs.Value);
        insert.Parameters.AddWithValue("$title", document.Title);
        insert.Parameters.AddWithValue("$released", document.Released ? 1 : 0);
        insert.Parameters.AddWithValue("$sensitive", document.Sensitive ? 1 : 0);
        insert.Parameters.AddWithValue("$content", document.Content);

        await insert.ExecuteNonQueryAsync(cancel);
    }

    /// <summary>Everything in the archive, for the measurement.</summary>
    /// <param name="cancel">To give up.</param>
    /// <returns>Every document, whoever it belongs to.</returns>
    /// <remarks>
    /// This is the method the portal must never reach for, and it is here
    /// because the measurement has to be able to ask questions the portal
    /// cannot. It is not on <see cref="IDocuments"/>, so nothing that only
    /// knows the interface can call it.
    /// </remarks>
    public async Task<IReadOnlyList<Document>> Everything(CancellationToken cancel = default)
    {
        await using var query = database.CreateCommand();
        query.CommandText = "SELECT id, belongs, title, released, sensitive, content FROM documents ORDER BY id";

        var found = new List<Document>();
        await using var rows = await query.ExecuteReaderAsync(cancel);

        while (await rows.ReadAsync(cancel))
        {
            found.Add(new Document(
                new DocumentId(rows.GetString(0)),
                new PatientId(rows.GetString(1)),
                rows.GetString(2),
                rows.GetInt32(3) == 1,
                rows.GetInt32(4) == 1,
                (byte[])rows[5]));
        }

        return found;
    }

    /// <inheritdoc />
    public void Dispose() => database.Dispose();

    private async Task<bool> Exists(DocumentId what, CancellationToken cancel)
    {
        await using var query = database.CreateCommand();
        query.CommandText = "SELECT EXISTS (SELECT 1 FROM documents WHERE id = $what)";
        query.Parameters.AddWithValue("$what", what.Value);

        return Convert.ToInt32(await query.ExecuteScalarAsync(cancel)) == 1;
    }

    private static string Schema()
    {
        var here = Assembly.GetExecutingAssembly();
        var name = here.GetManifestResourceNames().Single(one => one.EndsWith("schema.sql", StringComparison.Ordinal));

        using var file = here.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("the schema is not in the assembly");

        using var read = new StreamReader(file);

        return read.ReadToEnd();
    }
}
