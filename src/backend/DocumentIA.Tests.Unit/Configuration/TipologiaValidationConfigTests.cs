#nullable disable
using DocumentIA.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace DocumentIA.Tests.Unit.Configuration
{
    /// <summary>
    /// Tests para modelos de configuración de validación
    /// Valida serialización/deserialización y estructura de datos
    /// </summary>
    public class TipologiaValidationConfigTests
    {
        #region TipologiaValidationConfig Tests

        [Fact]
        public void TipologiaValidationConfig_DefaultValues_InitializeCorrectly()
        {
            // Arrange & Act
            var config = new TipologiaValidationConfig();

            // Assert
            config.TipologiaId.Should().BeEmpty();
            config.TipologiaNombre.Should().BeEmpty();
            config.Version.Should().BeEmpty();
            config.Fields.Should().NotBeNull();
            config.Fields.Should().BeEmpty();
        }

        [Fact]
        public void TipologiaValidationConfig_SetProperties_StoresValuesCorrectly()
        {
            // Arrange & Act
            var config = new TipologiaValidationConfig
            {
                TipologiaId = "notasimple",
                TipologiaNombre = "Nota Simple",
                Version = "1.4"
            };

            // Assert
            config.TipologiaId.Should().Be("notasimple");
            config.TipologiaNombre.Should().Be("Nota Simple");
            config.Version.Should().Be("1.4");
        }

        [Fact]
        public void TipologiaValidationConfig_AddFields_ListGrows()
        {
            // Arrange
            var config = new TipologiaValidationConfig();
            var field = new FieldValidationConfig { Name = "Campo1", Type = "string", Required = true };

            // Act
            config.Fields.Add(field);

            // Assert
            config.Fields.Should().HaveCount(1);
            config.Fields[0].Name.Should().Be("Campo1");
        }

        [Fact]
        public void TipologiaValidationConfig_MultipleFields_AllStored()
        {
            // Arrange & Act
            var config = new TipologiaValidationConfig();
            config.Fields.Add(new FieldValidationConfig { Name = "Campo1", Type = "string" });
            config.Fields.Add(new FieldValidationConfig { Name = "Campo2", Type = "decimal" });
            config.Fields.Add(new FieldValidationConfig { Name = "Campo3", Type = "date" });

            // Assert
            config.Fields.Should().HaveCount(3);
            config.Fields[0].Name.Should().Be("Campo1");
            config.Fields[1].Name.Should().Be("Campo2");
            config.Fields[2].Name.Should().Be("Campo3");
        }

        #endregion

        #region FieldValidationConfig Tests

        [Fact]
        public void FieldValidationConfig_DefaultValues_InitializeCorrectly()
        {
            // Arrange & Act
            var field = new FieldValidationConfig();

            // Assert
            field.Name.Should().BeEmpty();
            field.Type.Should().BeEmpty();
            field.Required.Should().BeFalse();
            field.Rules.Should().NotBeNull();
            field.Rules.Should().BeEmpty();
            field.Items.Should().BeNull();
        }

        [Fact]
        public void FieldValidationConfig_SetProperties_StoresValuesCorrectly()
        {
            // Arrange & Act
            var field = new FieldValidationConfig
            {
                Name = "Titulo",
                Type = "string",
                Required = true
            };

            // Assert
            field.Name.Should().Be("Titulo");
            field.Type.Should().Be("string");
            field.Required.Should().BeTrue();
        }

        [Fact]
        public void FieldValidationConfig_WithRules_StoresAllRules()
        {
            // Arrange & Act
            var field = new FieldValidationConfig { Name = "Campo", Type = "string" };
            field.Rules.Add(new ValidationRuleConfig { RuleType = "minlength" });
            field.Rules.Add(new ValidationRuleConfig { RuleType = "maxlength" });

            // Assert
            field.Rules.Should().HaveCount(2);
            field.Rules[0].RuleType.Should().Be("minlength");
            field.Rules[1].RuleType.Should().Be("maxlength");
        }

        [Fact]
        public void FieldValidationConfig_ArrayType_HasItems()
        {
            // Arrange & Act
            var field = new FieldValidationConfig
            {
                Name = "Items",
                Type = "array",
                Items = new ItemsConfig { Type = "object" }
            };

            // Assert
            field.Type.Should().Be("array");
            field.Items.Should().NotBeNull();
            field.Items.Type.Should().Be("object");
        }

        [Fact]
        public void FieldValidationConfig_ArrayWithProperties_HasNestedFields()
        {
            // Arrange
            var field = new FieldValidationConfig
            {
                Name = "Cargas",
                Type = "array",
                Items = new ItemsConfig { Type = "object" }
            };

            // Act
            field.Items.Properties.Add(new FieldValidationConfig { Name = "tipo", Type = "string" });
            field.Items.Properties.Add(new FieldValidationConfig { Name = "descripcion", Type = "string" });

            // Assert
            field.Items.Properties.Should().HaveCount(2);
            field.Items.Properties[0].Name.Should().Be("tipo");
            field.Items.Properties[1].Name.Should().Be("descripcion");
        }

        [Fact]
        public void FieldValidationConfig_RequiredField_FlagSet()
        {
            // Arrange & Act
            var field = new FieldValidationConfig
            {
                Name = "Obligatorio",
                Type = "string",
                Required = true
            };

            // Assert
            field.Required.Should().BeTrue();
        }

        [Fact]
        public void FieldValidationConfig_OptionalField_FlagNotSet()
        {
            // Arrange & Act
            var field = new FieldValidationConfig
            {
                Name = "Opcional",
                Type = "string",
                Required = false
            };

            // Assert
            field.Required.Should().BeFalse();
        }

        #endregion

        #region ValidationRuleConfig Tests

        [Fact]
        public void ValidationRuleConfig_DefaultValues_InitializeCorrectly()
        {
            // Arrange & Act
            var rule = new ValidationRuleConfig();

            // Assert
            rule.RuleType.Should().BeEmpty();
            rule.Severity.Should().BeEmpty();
            rule.Parameters.Should().NotBeNull();
            rule.Parameters.Should().BeEmpty();
        }

        [Fact]
        public void ValidationRuleConfig_SetProperties_StoresValuesCorrectly()
        {
            // Arrange & Act
            var rule = new ValidationRuleConfig
            {
                RuleType = "minlength",
                Severity = "Error"
            };

            // Assert
            rule.RuleType.Should().Be("minlength");
            rule.Severity.Should().Be("Error");
        }

        [Fact]
        public void ValidationRuleConfig_AddParameters_StoresAllValues()
        {
            // Arrange & Act
            var rule = new ValidationRuleConfig { RuleType = "minlength" };
            rule.Parameters.Add("value", 5);
            rule.Parameters.Add("message", "Too short");

            // Assert
            rule.Parameters.Should().HaveCount(2);
            rule.Parameters["value"].Should().Be(5);
            rule.Parameters["message"].Should().Be("Too short");
        }

        [Fact]
        public void ValidationRuleConfig_WithNullParameters_HandlesGracefully()
        {
            // Arrange & Act
            var rule = new ValidationRuleConfig
            {
                RuleType = "nif",
                Severity = "Warning",
                Parameters = null
            };

            // Assert - Should create new empty dict when accessed
            rule.Parameters.Should().BeNull();
        }

        [Fact]
        public void ValidationRuleConfig_AllSeverities_SetCorrectly()
        {
            // Arrange & Act & Assert - Error
            var errorRule = new ValidationRuleConfig { Severity = "Error" };
            errorRule.Severity.Should().Be("Error");

            // Warning
            var warningRule = new ValidationRuleConfig { Severity = "Warning" };
            warningRule.Severity.Should().Be("Warning");

            // Info
            var infoRule = new ValidationRuleConfig { Severity = "Info" };
            infoRule.Severity.Should().Be("Info");
        }

        [Fact]
        public void ValidationRuleConfig_RangeRuleType_StoresMinMax()
        {
            // Arrange & Act
            var rule = new ValidationRuleConfig
            {
                RuleType = "range",
                Severity = "Error"
            };
            rule.Parameters["min"] = 10;
            rule.Parameters["max"] = 100;

            // Assert
            rule.RuleType.Should().Be("range");
            rule.Parameters["min"].Should().Be(10);
            rule.Parameters["max"].Should().Be(100);
        }

        [Fact]
        public void ValidationRuleConfig_EnumRuleType_StoresValues()
        {
            // Arrange & Act
            var rule = new ValidationRuleConfig
            {
                RuleType = "enum",
                Severity = "Error"
            };
            rule.Parameters["values"] = new List<string> { "Activo", "Inactivo", "Pendiente" };
            rule.Parameters["caseSensitive"] = false;

            // Assert
            rule.RuleType.Should().Be("enum");
            rule.Parameters["values"].Should().BeOfType<List<string>>();
            rule.Parameters["caseSensitive"].Should().Be(false);
        }

        [Fact]
        public void ValidationRuleConfig_DateRuleType_StoresFormats()
        {
            // Arrange & Act
            var rule = new ValidationRuleConfig
            {
                RuleType = "date",
                Severity = "Error"
            };
            rule.Parameters["formats"] = new[] { "dd/MM/yyyy", "yyyy-MM-dd" };
            rule.Parameters["allowFuture"] = false;
            rule.Parameters["allowPast"] = true;

            // Assert
            rule.RuleType.Should().Be("date");
            var formats = rule.Parameters["formats"] as string[];
            formats.Should().HaveCount(2);
            formats[0].Should().Be("dd/MM/yyyy");
        }

        [Fact]
        public void ValidationRuleConfig_RegexRuleType_StoresPattern()
        {
            // Arrange & Act
            var rule = new ValidationRuleConfig
            {
                RuleType = "regex",
                Severity = "Error"
            };
            rule.Parameters["pattern"] = @"^\d{5}$";

            // Assert
            rule.RuleType.Should().Be("regex");
            rule.Parameters["pattern"].Should().Be(@"^\d{5}$");
        }

        #endregion

        #region ItemsConfig Tests

        [Fact]
        public void ItemsConfig_DefaultValues_InitializeCorrectly()
        {
            // Arrange & Act
            var items = new ItemsConfig();

            // Assert
            items.Type.Should().BeEmpty();
            items.Properties.Should().NotBeNull();
            items.Properties.Should().BeEmpty();
        }

        [Fact]
        public void ItemsConfig_SetType_StoresValue()
        {
            // Arrange & Act
            var items = new ItemsConfig { Type = "object" };

            // Assert
            items.Type.Should().Be("object");
        }

        [Fact]
        public void ItemsConfig_AddProperties_StoresNestedFields()
        {
            // Arrange
            var items = new ItemsConfig { Type = "object" };

            // Act
            items.Properties.Add(new FieldValidationConfig { Name = "tipo", Type = "string", Required = true });
            items.Properties.Add(new FieldValidationConfig { Name = "cantidad", Type = "decimal", Required = false });

            // Assert
            items.Properties.Should().HaveCount(2);
            items.Properties[0].Name.Should().Be("tipo");
            items.Properties[1].Name.Should().Be("cantidad");
        }

        [Fact]
        public void ItemsConfig_NestedArrayItems_SupportsRecursion()
        {
            // Arrange
            var parentItems = new ItemsConfig { Type = "object" };
            var nestedField = new FieldValidationConfig
            {
                Name = "SubItems",
                Type = "array",
                Items = new ItemsConfig { Type = "object" }
            };

            // Act
            parentItems.Properties.Add(nestedField);
            nestedField.Items.Properties.Add(new FieldValidationConfig { Name = "subfield", Type = "string" });

            // Assert
            parentItems.Properties.Should().HaveCount(1);
            parentItems.Properties[0].Items.Properties.Should().HaveCount(1);
            parentItems.Properties[0].Items.Properties[0].Name.Should().Be("subfield");
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void CompleteConfig_RealWorldNotaSimple_StructureMatches()
        {
            // Arrange & Act - Construir configuración realista
            var config = new TipologiaValidationConfig
            {
                TipologiaId = "notasimple",
                TipologiaNombre = "Nota Simple",
                Version = "1.4"
            };

            // Agregar campo simple con reglas
            var fincaField = new FieldValidationConfig
            {
                Name = "FincaRegistral",
                Type = "string",
                Required = true
            };
            fincaField.Rules.Add(new ValidationRuleConfig
            {
                RuleType = "minlength",
                Severity = "Error",
                Parameters = new Dictionary<string, object?> { { "value", 1 } }
            });
            fincaField.Rules.Add(new ValidationRuleConfig
            {
                RuleType = "maxlength",
                Severity = "Error",
                Parameters = new Dictionary<string, object?> { { "value", 30 } }
            });
            config.Fields.Add(fincaField);

            // Agregar campo con enum
            var derechoField = new FieldValidationConfig
            {
                Name = "DerechoTitularidad",
                Type = "string",
                Required = true
            };
            derechoField.Rules.Add(new ValidationRuleConfig
            {
                RuleType = "enum",
                Severity = "Error",
                Parameters = new Dictionary<string, object?>
                {
                    { "values", new List<string> { "Pleno dominio", "Nuda propiedad", "Usufructo" } }
                }
            });
            config.Fields.Add(derechoField);

            // Agregar array de objetos (Cargas)
            var cargasField = new FieldValidationConfig
            {
                Name = "Cargas",
                Type = "array",
                Required = false,
                Items = new ItemsConfig { Type = "object" }
            };
            cargasField.Items.Properties.Add(new FieldValidationConfig
            {
                Name = "tipo",
                Type = "string",
                Required = true
            });
            cargasField.Items.Properties.Add(new FieldValidationConfig
            {
                Name = "descripcion",
                Type = "string",
                Required = false
            });
            config.Fields.Add(cargasField);

            // Assert
            config.TipologiaId.Should().Be("notasimple");
            config.Fields.Should().HaveCount(3);

            // Verificar campo simple
            config.Fields[0].Name.Should().Be("FincaRegistral");
            config.Fields[0].Rules.Should().HaveCount(2);

            // Verificar enum
            config.Fields[1].Name.Should().Be("DerechoTitularidad");
            config.Fields[1].Rules[0].RuleType.Should().Be("enum");

            // Verificar array
            config.Fields[2].Name.Should().Be("Cargas");
            config.Fields[2].Type.Should().Be("array");
            config.Fields[2].Items.Should().NotBeNull();
            config.Fields[2].Items.Properties.Should().HaveCount(2);
        }

        [Fact]
        public void CompleteConfig_DocumentoTasacion_MultiFieldStructure()
        {
            // Arrange & Act
            var config = new TipologiaValidationConfig
            {
                TipologiaId = "tasacion",
                TipologiaNombre = "Tasación",
                Version = "1.0"
            };

            // Campo obligatorio con regex
            var refCataField = new FieldValidationConfig
            {
                Name = "ReferenciaCatastral",
                Type = "string",
                Required = true
            };
            refCataField.Rules.Add(new ValidationRuleConfig
            {
                RuleType = "catastral",
                Severity = "Error"
            });
            config.Fields.Add(refCataField);

            // Campo fecha
            var fechaField = new FieldValidationConfig
            {
                Name = "FechaTasacion",
                Type = "date",
                Required = true
            };
            fechaField.Rules.Add(new ValidationRuleConfig
            {
                RuleType = "date",
                Severity = "Error",
                Parameters = new Dictionary<string, object?>
                {
                    { "formats", new[] { "dd/MM/yyyy" } },
                    { "allowFuture", false },
                    { "allowPast", true }
                }
            });
            config.Fields.Add(fechaField);

            // Campo rango numérico
            var valorField = new FieldValidationConfig
            {
                Name = "ValorTasado",
                Type = "decimal",
                Required = true
            };
            valorField.Rules.Add(new ValidationRuleConfig
            {
                RuleType = "range",
                Severity = "Error",
                Parameters = new Dictionary<string, object?>
                {
                    { "min", 5000m },
                    { "max", 500000m }
                }
            });
            config.Fields.Add(valorField);

            // Assert
            config.Fields.Should().HaveCount(3);
            config.Fields[0].Rules[0].RuleType.Should().Be("catastral");
            config.Fields[1].Rules[0].RuleType.Should().Be("date");
            config.Fields[2].Rules[0].RuleType.Should().Be("range");
        }

        [Fact]
        public void CompleteConfig_AllRuleTypes_Representable()
        {
            // Arrange & Act - Una config con todos los tipos de reglas
            var config = new TipologiaValidationConfig { TipologiaId = "all_rules" };

            var ruleTypes = new[] { "range", "nif", "catastral", "date", "address", "enum", "regex", "minlength", "maxlength" };
            foreach (var ruleType in ruleTypes)
            {
                var field = new FieldValidationConfig { Name = $"Field_{ruleType}", Type = "string" };
                field.Rules.Add(new ValidationRuleConfig { RuleType = ruleType, Severity = "Error" });
                config.Fields.Add(field);
            }

            // Assert
            config.Fields.Should().HaveCount(ruleTypes.Length);
            for (int i = 0; i < ruleTypes.Length; i++)
            {
                config.Fields[i].Rules[0].RuleType.Should().Be(ruleTypes[i]);
            }
        }

        #endregion
    }
}
