# Third-Party Notices and Licenses

Status: RECONCILED

DX-OS incorporates or depends on third-party software components. This document provides the required copyright notices, permission notices, and license texts for those components. This document does not replace or modify the project license for DX-OS source code; see [LICENSE](LICENSE) and [ADR-0001](docs/adr/0001-dx-os-open-source-license.md).

The machine-readable inventory of all dependencies is maintained in [OPEN_SOURCE.md](OPEN_SOURCE.md) and [artifacts/sbom.cdx.json](artifacts/sbom.cdx.json).

---

## 1. MIT License (MIT)

Used by:
- AIECOS Social CRM foundation (https://github.com/aiecosvietnam/aiecos-social-crm), Copyright (c) 2026 AIECOS. Full license text: [LICENSES/MIT-AIECOS.txt](LICENSES/MIT-AIECOS.txt). Paths: `chrome-extension/`, `sync-receiver/`, `admin-ui/`, `mcp-server/`, `examples/`, `docs/aiecos/`.
- Elsa Workflows (Elsa 3.7.1)
- Microsoft .NET Libraries (Microsoft.NET.Test.Sdk 17.13.0, Microsoft.EntityFrameworkCore 10.0.10, Aspire.Hosting.* 13.4.6)
- Testcontainers for .NET (Testcontainers.PostgreSql 4.13.0)
- SSH.NET (SSH.NET 2026.0.0)
- Gitleaks (Gitleaks 8.30.0)

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 2. Apache License 2.0 (Apache-2.0)

Used by:
- xUnit.net (xunit.v3 3.2.2)
- ArchUnitNET (TngTech.ArchUnitNET 0.13.3, TngTech.ArchUnitNET.xUnitV3 0.13.3)
- Trivy (Trivy 0.72.0)
- Syft (Syft 1.50.0)
- Grype (Grype 0.116.1)

```
                                 Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

1. Definitions.
   "License" shall mean the terms and conditions for use, reproduction,
   and distribution as defined by Sections 1 through 9 of this document.

   "Licensor" shall mean the copyright owner or entity authorized by
   the copyright owner that is granting the License.

   "Legal Entity" shall mean the union of the acting entity and all
   other entities that control, are controlled by, or are under common
   control with that entity.

   "You" (or "Your") shall mean an individual or Legal Entity
   exercising permissions granted by this License.

2. Grant of Copyright License. Subject to the terms and conditions of
   this License, each Contributor hereby grants to You a perpetual,
   worldwide, non-exclusive, no-charge, royalty-free, irrevocable
   copyright license to reproduce, prepare Derivative Works of,
   publicly display, publicly perform, sublicense, and distribute the
   Work and such Derivative Works in Source or Object form.

3. Grant of Patent License. Subject to the terms and conditions of
   this License, each Contributor hereby grants to You a perpetual,
   worldwide, non-exclusive, no-charge, royalty-free, irrevocable
   patent license to make, have made, use, offer to sell, sell, import,
   and otherwise transfer the Work.

4. Redistribution. You may reproduce and distribute copies of the
   Work or Derivative Works thereof in any medium, with or without
   modifications, and in Source or Object form, provided that You
   meet the following conditions:

   (a) You must give any other recipients of the Work or
       Derivative Works a copy of this License; and
   (b) You must cause any modified files to carry prominent notices
       stating that You changed the files; and
   (c) You must retain, in the Source form of any Derivative Works
       that You distribute, all copyright, patent, trademark, and
       attribution notices from the Source form of the Work; and
   (d) If the Work includes a "NOTICE" text file as part of its
       distribution, then any Derivative Works that You distribute must
       include a readable copy of the attribution notices contained
       within such NOTICE file.

5. Disclaimer of Warranty. Unless required by applicable law or
   agreed to in writing, Licensor provides the Work (and each
   Contributor provides its Contributions) on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
   implied, including, without limitation, any warranties or conditions
   of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
   PARTICULAR PURPOSE.
```

---

## 3. The PostgreSQL License

Used by:
- Npgsql Entity Framework Core PostgreSQL Provider (Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3)
- PostgreSQL Container (postgres:18.4-alpine)

```
Portions Copyright (c) 1996-2026, PostgreSQL Global Development Group
Portions Copyright (c) 1994, The Regents of the University of California

Permission to use, copy, modify, and distribute this software and its
documentation for any purpose, without fee, and without a written agreement
is hereby granted, provided that the above copyright notice and this
paragraph and the following two paragraphs appear in all copies.

IN NO EVENT SHALL THE UNIVERSITY OF CALIFORNIA BE LIABLE TO ANY PARTY FOR
DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, INCLUDING
LOST PROFITS, ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS
DOCUMENTATION, EVEN IF THE UNIVERSITY OF CALIFORNIA HAS BEEN ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.

THE UNIVERSITY OF CALIFORNIA SPECIFICALLY DISCLAIMS ANY WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
AND FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS
ON AN "AS IS" BASIS, AND THE UNIVERSITY OF CALIFORNIA HAS NO OBLIGATIONS TO
PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
```

---

## 4. Reused / Adapted Source

No copied, adapted, vendored, or forked source is present in the DX-OS runtime at the current bootstrap stage. All runtime capabilities are consumed strictly via verified package dependencies or built from first principles.
