# Plan: Azure Content Understanding for Tipologias

## Objective

Replace the hardcoded mock extraction path for `nota.simple.1_4` with a configuration-driven Azure AI Content Understanding integration that:

- resolves the analyzer model by configuration
- binds analyzers to tipologias without code changes per tipologia
- maps analyzer output into `DatosExtraidos`
- keeps existing tipologias compatible and updatable

## Decisions

- Introduce a typed extraction contract instead of passing only `tipologia`
- Use a composite extraction provider that routes by tipologia configuration
- Store tipologia extraction binding in `*.validation.json`
- Store reusable model registry in `config/extraction/models.json`
- Normalize Content Understanding results generically from analyzer field types to plain CLR objects

## Execution Steps

1. Extend extraction models and provider contracts
2. Add extraction config to tipologia configuration model
3. Add model registry loader for analyzer definitions
4. Implement Azure Content Understanding provider
5. Implement provider routing by tipologia extraction config
6. Update `nota.simple.1_4` to use Azure Content Understanding
7. Add tests for config loading and result mapping
8. Build and run targeted tests

## Status

- [x] Analysis complete
- [x] Design approved
- [ ] Implementation in progress
- [ ] Validation pending