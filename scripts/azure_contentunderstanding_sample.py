"""
Run Azure Content Understanding analyze (SDK sample adapted).
Usage:
  python azure_contentunderstanding_sample.py --endpoint <endpoint> --key <api_key> --file-url <file_url> [--analyzer CU_NS_1.4_2]

Requires:
  pip install azure-ai-contentunderstanding azure-identity

If you don't want to pass the key as an arg, set env var CONTENT_UNDERSTANDING_KEY.
"""
import argparse
import json
import os
import sys

from azure.ai.contentunderstanding import ContentUnderstandingClient
from azure.ai.contentunderstanding.models import AnalysisInput
from azure.core.credentials import AzureKeyCredential
from azure.identity import DefaultAzureCredential
from azure.core.exceptions import AzureError


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--endpoint", required=True, help="Content Understanding endpoint, e.g. https://...services.ai.azure.com")
    p.add_argument("--key", required=False, help="API key (optional if using DefaultAzureCredential)")
    p.add_argument("--file-url", required=True, help="Public or SAS URL of the file to analyze")
    p.add_argument("--analyzer", default="CU_NS_1.4_2", help="Analyzer id to use")
    args = p.parse_args()

    endpoint = args.endpoint.rstrip('/')
    key = args.key or os.environ.get("CONTENT_UNDERSTANDING_KEY")
    file_url = args.file_url
    analyzer_id = args.analyzer
    api_version = "2025-11-01"

    credential = AzureKeyCredential(key) if key else DefaultAzureCredential()
    client = ContentUnderstandingClient(endpoint=endpoint, credential=credential, api_version=api_version)

    print(f"Analyzing with analyzer '{analyzer_id}' -> {file_url}")
    try:
        poller = client.begin_analyze(analyzer_id=analyzer_id, inputs=[AnalysisInput(url=file_url)])
        result = poller.result()
    except AzureError as err:
        print("[Azure Error]:", err)
        sys.exit(1)
    except Exception as ex:
        print("[Unexpected Error]:", ex)
        sys.exit(1)

    result_dict = result.as_dict()
    print(json.dumps(result_dict, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
