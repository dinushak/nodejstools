﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NodeReferenceGenerator {

    class NodeReferenceGenerator {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private readonly StringBuilder _output = new StringBuilder();
        private readonly dynamic _all;

        public NodeReferenceGenerator() {
            _all = _serializer.DeserializeObject(File.ReadAllText("all.json"));
        }

        static void Main(string[] args) {
            var res = new NodeReferenceGenerator().Generate();
            Console.WriteLine(res);
            File.WriteAllText("all.js", res);
        }

        private string Generate() {
            _output.Append("function require(module) {\r\n");

            GenerateRequireBody(_output, _all);

            _output.Append("// **NTVS** INSERT USER MODULE SWITCH HERE **NTVS**");

            _output.Append(@"
            
    }
}
");

            foreach (var misc in _all["miscs"]) {
                if (misc["name"] == "Global Objects") {
                    GenerateGlobals(misc["globals"]);
                    break;
                }
            }

            return _output.ToString();
        }

        private void GenerateGlobals(dynamic globals) {
            foreach (var global in globals) {
                if (global.ContainsKey("events") ||
                    global.ContainsKey("methods") ||
                    global.ContainsKey("properties") ||
                    global.ContainsKey("classes")) {


                    _output.AppendFormat("var {0} = new ", global["name"]);
                    GenerateModuleWorker(global, 0, "__" + global["name"]);
                    _output.Append(";");
                }
            }
        }

        private void GenerateModule(dynamic module, int indentation = 0) {
            string modName = FixModuleName(module["name"]);

            GenerateModuleWorker(module, indentation, modName);
        }

        private void GenerateModuleWorker(dynamic module, int indentation, string name) {
            _output.Append(' ', indentation * 4);
            _output.AppendFormat("function {0}() {{\r\n", name);

            if (module.ContainsKey("desc")) {
                _output.Append(' ', (indentation + 1) * 4);
                _output.AppendFormat("/// <summary>{0}</summary>\r\n", FixDescription(module["desc"]));
            }

            if (module.ContainsKey("methods")) {
                foreach (var method in module["methods"]) {
                    GenerateMethod(name, method, indentation + 1);
                }
            }

            if (module.ContainsKey("events")) {
                GenerateEvents(module["events"], indentation + 1);
            }

            if (module.ContainsKey("classes")) {
                foreach (var klass in module["classes"]) {
                    GenerateClass(name, klass, indentation + 1);
                }
            }

            if (module.ContainsKey("properties")) {
                GenerateProperties(module["properties"], indentation + 1);
            }

            _output.AppendFormat("}}", name);
        }

        private void GenerateClass(string modName, dynamic klass, int indentation) {
            string className = FixClassName(klass["name"]);
            _output.Append(' ', indentation * 4);
            _output.AppendFormat("this.{0} = function() {{\r\n", className);

            if (klass.ContainsKey("methods")) {
                foreach (var method in klass["methods"]) {
                    GenerateMethod(modName + "." + className, method, indentation + 1);
                }
            }

            if (klass.ContainsKey("events")) {
                GenerateEvents(klass["events"], indentation + 1);
            }

            if (klass.ContainsKey("properties")) {
                GenerateProperties(klass["properties"], indentation + 1);
            }

            _output.Append(' ', indentation * 4);
            _output.AppendLine("}");
        }

        private void GenerateProperties(dynamic properties, int indentation) {
            foreach (var prop in properties) {
                string desc = "";
                if (prop.ContainsKey("desc")) {
                    desc = prop["desc"];

                    _output.Append(' ', indentation * 4);
                    _output.AppendFormat("/// <field name='{0}'>{1}</field>\r\n",
                        prop["name"],
                        FixDescription(desc));

                }

                _output.Append(' ', indentation * 4);
                if (desc.IndexOf("<code>Boolean</code>") != -1) {
                    _output.AppendFormat("this.{0} = true;\r\n", prop["name"]);
                } else if (desc.IndexOf("<code>Number</code>") != -1) {
                    _output.AppendFormat("this.{0} = 0;\r\n", prop["name"]);
                } else if (prop.ContainsKey("textRaw")) {
                    string textRaw = prop["textRaw"];
                    int start, end;
                    if ((start = textRaw.IndexOf('{')) != -1 && (end = textRaw.IndexOf('}')) != -1 &&
                        start < end) {
                        string typeName = textRaw.Substring(start, end - start);
                        switch (typeName) {
                            case "Boolean":
                                _output.AppendFormat("this.{0} = true;\r\n", prop["name"]);
                                break;
                            case "Number":
                                _output.AppendFormat("this.{0} = true;\r\n", prop["name"]);
                                break;
                            default:
                                _output.AppendFormat("this.{0} = undefined;\r\n", prop["name"]);
                                break;
                        }
                    } else {
                        _output.AppendFormat("this.{0} = undefined;\r\n", prop["name"]);
                    }
                } else {
                    _output.AppendFormat("this.{0} = undefined;\r\n", prop["name"]);
                }
            }
        }

        private void GenerateEvents(dynamic events, int indentation) {
            _output.AppendLine("emitter = new Events().EventEmitter;");

            foreach (var ev in events) {
                if (ev["name"].IndexOf(' ') != -1) {
                    continue;
                }
                if (ev.ContainsKey("desc")) {
                    _output.Append(' ', indentation * 4);
                    _output.AppendFormat("/// <field name='{0}'>{1}</field>\r\n", ev["name"], FixDescription(ev["desc"]));

                }
                _output.Append(' ', indentation * 4);
                _output.AppendFormat("this.{0} = new emitter();\r\n", ev["name"]);
            }
        }

        private void GenerateMethod(string fullName, dynamic method, int indentation = 1) {
            _output.Append(' ', indentation * 4);
            _output.AppendFormat("this.{0} = function(", method["name"]);
            var signature = method["signatures"][0];
            bool first = true;
            foreach (var param in signature["params"]) {
                if (param["name"] == "...") {
                    break;
                }
                if (!first) {
                    _output.Append(", ");
                }

                _output.Append(FixModuleName(param["name"]));
                first = false;

                // TODO: Optional?
            }

            _output.Append(") {\r\n");

            if (method.ContainsKey("desc")) {
                _output.Append(' ', (indentation + 1) * 4);
                _output.AppendFormat("/// <summary>{0}</summary>\r\n", FixDescription(method["desc"]));
            }
            foreach (var curSig in method["signatures"]) {
                if (curSig["params"].Length > 0) {
                    _output.Append(' ', (indentation + 1) * 4);
                    _output.AppendLine("/// <signature>");

                    foreach (var param in curSig["params"]) {
                        _output.Append(' ', (indentation + 1) * 4);
                        _output.AppendFormat("/// <param name=\"{0}\"", param["name"]);

                        if (param.ContainsKey("type")) {
                            _output.AppendFormat(" type=\"{0}\"", FixModuleName(param["type"]));
                        }
                        _output.Append('>');

                        if (param.ContainsKey("desc")) {
                            _output.Append(param["desc"]);
                        }
                        _output.AppendLine("</param>");
                    }

                    if (curSig.ContainsKey("return")) {
                        object returnType = null, returnDesc = null;
                        if (curSig["return"].ContainsKey("type")) {
                            returnType = FixModuleName(curSig["return"]["type"]);
                        }

                        if (curSig["return"].ContainsKey("desc")) {
                            returnDesc = curSig["return"]["desc"];
                        }

                        if (returnType != null || returnDesc != null) {
                            _output.Append(' ', (indentation + 1) * 4);
                            _output.Append("/// <returns");

                            if (returnType != null) {
                                _output.AppendFormat(" type=\"{0}\">", returnType);
                            } else {
                                _output.Append('>');
                            }

                            if (returnDesc != null) {
                                _output.Append(returnDesc);
                            }
                            _output.AppendLine("</returns>");
                        }
                    }
                    _output.Append(' ', (indentation + 1) * 4);
                    _output.AppendLine("/// </signature>");
                }
            }

            if (method["name"].StartsWith("create") && method["name"].Length > 6) {
                _output.Append(' ', indentation * 4);
                _output.AppendFormat("return new {0}.{1}();", fullName, method["name"].Substring(6));
                _output.AppendLine();
            }
            _output.Append(' ', indentation * 4);
            _output.AppendLine("}");
        }

        private void GenerateRequireBody(StringBuilder res, dynamic all) {
            res.Append(@"
    switch (module) {
");



            foreach (var module in all["modules"]) {
                res.AppendFormat("        case \"{0}\": return new ", module["name"]);
                GenerateModule(module, 1);
                res.Append(";\r\n");
            }

        }


        private static dynamic FixModuleName(string module) {
            for (int i = 0; i < module.Length; i++) {
                if (!(Char.IsLetterOrDigit(module[i]) || module[i] == '_')) {
                    return module.Substring(0, i);
                }
            }
            return module;
        }

        private static string FixDescription(string desc) {
            return desc.Replace("\n", " ");
        }

        private string FixClassName(string name) {
            name = name.Replace(" ", "");
            int dot;
            if ((dot = name.IndexOf('.')) != -1) {
                return name.Substring(dot + 1);
            }
            return name;
        }

    }
}
