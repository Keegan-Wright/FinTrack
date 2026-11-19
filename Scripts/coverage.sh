#!/usr/bin/env bash
# Runs the tests with code coverage and writes an HTML report.
#
# Usage: Scripts/coverage.sh [--unit-only]
#
# Runs the unit tests, then the integration tests unless --unit-only is given. The integration tests need Docker,
# the same as running them without coverage. The app Aspire starts inherits the coverage profiler from the test run,
# so its code counts too. The report is written to TestResults/Coverage/report and opened when the machine can.
set -euo pipefail

repository="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="$repository/TestResults/Coverage"

usage() {
    echo "Usage: $0 [--unit-only]" >&2
    exit 1
}

unit_only=false
if [[ $# -eq 1 && "$1" == "--unit-only" ]]; then
    unit_only=true
elif [[ $# -ne 0 ]]; then
    usage
fi

cd "$repository"
rm -rf "$output"
dotnet tool restore

# One collector for every test run, each connecting to it, so they all land in one report
session="fintrack-coverage-$$"
dotnet dotnet-coverage collect --session-id "$session" --server-mode --background \
    --settings Test/CodeCoverage.xml --output-format cobertura --output "$output/coverage.cobertura.xml"

# Stops the collector, which writes the coverage, also when a test run fails
stop_collector() {
    dotnet dotnet-coverage shutdown "$session" > /dev/null
}
trap stop_collector EXIT

run_with_coverage() {
    dotnet dotnet-coverage connect --nologo "$session" dotnet test --project "$1"
}

for project in Client Domain FinanceTracker Infrastructure; do
    run_with_coverage "Test/FinanceTracker.Tests.Unit.$project"
done

if [[ "$unit_only" == false ]]; then
    run_with_coverage Test/FinanceTracker.Tests.Integration
fi

trap - EXIT
stop_collector

dotnet reportgenerator -reports:"$output/coverage.cobertura.xml" -targetdir:"$output/report" \
    -reporttypes:"Html;TextSummary"

# The totals and each assembly, the report has every class
summary="$output/report/Summary.txt"
grep -E "^  (Line|Branch|Method) coverage:" "$summary"
echo
grep -E "^[^ ].*[0-9]%$" "$summary"
echo
echo "Report: $output/report/index.html"

if command -v xdg-open > /dev/null; then
    xdg-open "$output/report/index.html" > /dev/null 2>&1 || true
elif command -v open > /dev/null; then
    open "$output/report/index.html" || true
fi
