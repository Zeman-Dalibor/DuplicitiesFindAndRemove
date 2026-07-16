# DuplicitiesRemoverDotNet

## Disk identity placeholder file

On startup the application derives a portable disk identity from the scanned path. It looks for a
placeholder file named `.duplicities-disk-id.json` in the disk root and creates it when missing.
The file contains a GUID (and optionally the volume label) that uniquely and portably identifies the
disk, independent of drive letter or mount point. This identity is stored in the database and reused
elsewhere in the application. It is read only once per disk, because a folder can never belong to
more than one disk at a time.

Example `.duplicities-disk-id.json`:

```json
{
  "Id": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "Label": "Backup Drive",
  "CreatedUtc": "2026-07-16T12:34:56.7891234Z",
  "Application": "DuplicitiesFindAndRemove"
}
```

`Label` is optional and omitted when the volume label is unavailable.