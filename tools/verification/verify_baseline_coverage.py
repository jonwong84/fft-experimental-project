"""Validate the planning inventory and reproduce its readable view (Python 3.11+, standard library)."""
import argparse
import json
import re
import subprocess
from collections import Counter
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "docs/coverage/baseline-coverage.json"
VIEW = ROOT / "docs/coverage/baseline-coverage.md"
# Resolve open headings and completed index links without loading chronological snapshots.
TICKET_DOCUMENTS = ("docs/BACKLOG.md", "docs/history/README.md")
DOMAINS = ("simulation", "catalog", "campaign", "screens", "presentation", "assets", "persistence", "validation", "profiles")
SCOPES = {"baseline", "platform-replacement", "edition-audit", "extension", "optional-interoperability"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique_fields(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, f"Duplicate JSON field: {key}")
        result[key] = value
    return result


def relative_file(base, value):
    require(isinstance(value, str) and value and "\\" not in value and ":" not in value,
            f"Expected a relative forward-slash file path: {value!r}")
    path = PurePosixPath(value)
    require(not path.is_absolute() and ".." not in path.parts, f"Unsafe inventory path: {value}")
    require((base / value).is_file(), f"Missing file under {base}: {value}")


def validate(data, reference_root=None):
    require(data["format"] == "fft.baseline-coverage" and type(data["schemaVersion"]) is int and data["schemaVersion"] == 1,
            "Unsupported planning-inventory format/version")
    require(data["edition"] == "SCUS-94221", "Unexpected baseline edition")
    require(re.fullmatch(r"[0-9a-f]{40}", data["reference"]["commit"]), "Reference requires an exact commit")
    require(data["reference"]["readOnly"] is True, "Reference must remain read-only")
    require(type(data["lastRecordedTests"]["runForThisInventory"]) is bool, "Test-run provenance must be explicit")
    relative_file(ROOT, data["lastRecordedTests"]["source"])
    tickets = {ticket for document in TICKET_DOCUMENTS
               for ticket in re.findall(r"^(?:### |- \[)(ZOD-\d+) —", (ROOT / document).read_text(encoding="utf-8"), re.M)}
    seen = set()
    fields = {"id", "domain", "title", "profileScope", "ownerTicket", "ownerArea", "status", "currentScope",
              "referenceSources", "evidenceDocs", "implementationPaths", "automatedChecks", "manualCheckpoint", "openQuestions", "nextAction"}
    if reference_root:
        observed = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=reference_root, text=True).strip()
        require(observed == data["reference"]["commit"], f"Reference moved: expected {data['reference']['commit']}, got {observed}")
    for row in data["rows"]:
        require(fields <= set(row) <= fields | {"followUpTickets"}, f"Unexpected/missing fields: {row.get('id')}")
        identifier = row["id"]
        require(isinstance(identifier, str) and re.fullmatch(r"COV-[A-Z]+-\d{3}", identifier), f"Invalid coverage ID: {identifier}")
        require(identifier not in seen, f"Duplicate coverage ID: {identifier}")
        seen.add(identifier)
        require(row["domain"] in DOMAINS and row["profileScope"] in SCOPES, f"Invalid domain/profile scope: {identifier}")
        require(row["status"] in data["statusDefinitions"], f"Unknown status: {identifier}")
        for ticket in [row["ownerTicket"], *row.get("followUpTickets", [])]:
            require(ticket in tickets, f"Unknown backlog ticket {ticket}: {identifier}")
        for field in ("title", "ownerArea", "currentScope", "nextAction"):
            require(isinstance(row[field], str) and row[field].strip(), f"Empty {field}: {identifier}")
        require(isinstance(row["openQuestions"], list) and row["openQuestions"] and all(isinstance(x, str) and x.strip() for x in row["openQuestions"]),
                f"Every row needs explicit remaining gaps: {identifier}")
        require(row["referenceSources"] and row["evidenceDocs"], f"Missing source/requirement evidence: {identifier}")
        for source in row["referenceSources"]:
            require(set(source) == {"path", "inspection"} and source["inspection"], f"Missing inspection scope: {identifier}")
            if reference_root:
                relative_file(reference_root, source["path"])
            else:
                path = PurePosixPath(source["path"])
                require(not path.is_absolute() and ".." not in path.parts and ":" not in source["path"] and "\\" not in source["path"],
                        f"Unsafe reference source path: {identifier}")
        for path in row["evidenceDocs"] + row["implementationPaths"]:
            relative_file(ROOT, path)
        for check in row["automatedChecks"]:
            relative_file(ROOT, check["path"])
            require(check["scope"], f"Missing automated-check scope: {identifier}")
        manual = row["manualCheckpoint"]
        require(manual["status"] and manual["procedure"], f"Missing manual checkpoint: {identifier}")
        if "evidence" in manual:
            relative_file(ROOT, manual["evidence"])
    require(set(row["domain"] for row in data["rows"]) == set(DOMAINS), "Missing a major inventory domain")


def project_link(path):
    return f"[{path}](../../{path})"


def render(data):
    lines = ["# Baseline coverage entries", "", "Generated from `baseline-coverage.json` by `tools/verification/verify_baseline_coverage.py --write-view`.",
             "Edit the JSON, then regenerate this view. See [the inventory guide](README.md) for status/evidence rules.", "",
             f"Target: **{data['edition']}**. Snapshot: {data['updated']}; managed base `{data['managedCommit']}`; reference `{data['reference']['commit']}`.",
             "These entries are planning scopes, not a feature-completion percentage or a complete record/encounter catalog.", ""]
    for domain in DOMAINS:
        entries = [row for row in data["rows"] if row["domain"] == domain]
        lines.extend([f"## {domain.title()}", ""])
        for row in entries:
            lines.extend([f"### {row['id']} — {row['title']}", "",
                          f"Status: **{row['status']}**; scope: **{row['profileScope']}**; ticket: **{row['ownerTicket']}**; owner area: {row['ownerArea']}.", "",
                          row["currentScope"], "",
                          "Evidence: " + ", ".join(project_link(path) for path in row["evidenceDocs"]) + ".", "",
                          "Source entry points (pinned revision above; not full traces): " + ", ".join(
                              f"[{s['path']}](https://github.com/adamrt/fft_decomp/blob/{data['reference']['commit']}/{s['path']})" for s in row["referenceSources"]) + ".", ""])
            if row["implementationPaths"]:
                lines.extend(["Current implementation: " + ", ".join(project_link(path) for path in row["implementationPaths"]) + ".", ""])
            if row["automatedChecks"]:
                lines.extend(["Existing bounded checks: " + ", ".join(project_link(check["path"]) for check in row["automatedChecks"]) + ".", ""])
            else:
                lines.extend(["Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.", ""])
            manual = row["manualCheckpoint"]
            lines.extend([f"Manual checkpoint ({manual['status']}): {manual['procedure']}", "", "Remaining gaps:", ""])
            lines.extend("- " + gap for gap in row["openQuestions"])
            lines.extend(["", "Next action: " + row["nextAction"], ""])
            if row.get("followUpTickets"):
                lines.extend(["Bounded follow-ups: " + ", ".join(row["followUpTickets"]) + ".", ""])
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-root", type=Path, help="Optionally verify the read-only reference revision and every source path")
    parser.add_argument("--write-view", action="store_true", help="Regenerate the readable view from the reviewed JSON")
    parser.add_argument("--domain", choices=DOMAINS, help="Print only one planning domain after validating")
    parser.add_argument("--status", help="Print only rows with this exact status after validating")
    args = parser.parse_args()
    try:
        data = json.loads(DATA.read_text(encoding="utf-8"), object_pairs_hook=unique_fields)
        validate(data, args.reference_root)
        if args.status:
            require(args.status in data["statusDefinitions"], f"Unknown status filter: {args.status}")
        view = render(data)
        if args.write_view:
            VIEW.write_text(view, encoding="utf-8", newline="\n")
        else:
            require(VIEW.is_file() and VIEW.read_text(encoding="utf-8") == view, "Readable view is stale; review JSON and run --write-view")
        counts = Counter(row["domain"] for row in data["rows"])
        print(f"Validated {len(data['rows'])} planning entries across {len(counts)} domains; this checker does not execute .NET tests or establish baseline equivalence.")
        if args.domain or args.status:
            for row in data["rows"]:
                if (not args.domain or row["domain"] == args.domain) and (not args.status or row["status"] == args.status):
                    print(f"{row['id']} | {row['status']} | {row['ownerTicket']} | {row['title']}")
        return 0
    except (KeyError, TypeError, ValueError, OSError, subprocess.CalledProcessError) as error:
        print(f"Coverage validation failed: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
