# Deliverable Packaging Verification Rule

When rebuilding or repackaging any client deliverable (ZIP archive, video, PDF, etc.):

1. **Never assume success from exit code alone.** A packaging script completing without errors does NOT mean the package is correct — `Test-Path`, `Copy-Item`, and similar silent-fail commands will skip files on filename typos without throwing errors.

2. **Always verify the output directly:**
   - For ZIP files: list all entries with their sizes via `System.IO.Compression.ZipFile` or equivalent, and confirm every expected file is present.
   - For file copies: verify the destination contains what was expected.
   - For HTTP-served assets: test every referenced path returns HTTP 200.

3. **Cross-reference HTML internal references:** Parse `href` and `src` attributes from any HTML file and confirm every referenced relative path exists in the package.

4. **For end-to-end verification:** Extract the deliverable to a clean isolated directory (not the source directory), serve it, and test from there — exactly as the client would experience it.

5. **Report findings by direct evidence**, not assumption. Show the listing, the HTTP status codes, the file sizes.
