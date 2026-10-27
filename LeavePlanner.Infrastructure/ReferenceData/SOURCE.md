# ISO 3166-2 subdivision catalog source

`IsoSubdivisionData.g.cs` contains data derived from Ipregistry's
[`subdivisions.csv`](https://github.com/ipregistry/iso3166/blob/9f8c9fb33b9c9447bf7a1edd7b93a98b7355af61/subdivisions.csv).
The source is pinned to commit
[`9f8c9fb33b9c9447bf7a1edd7b93a98b7355af61`](https://github.com/ipregistry/iso3166/commit/9f8c9fb33b9c9447bf7a1edd7b93a98b7355af61),
dated **2026-08-26 09:10 UTC**.

The generated file retains distinct, uppercased full ISO 3166-2 codes only
from rows whose `parent_subdivision` field is empty. It contains 3,590 codes
from 4,570 eligible rows in the pinned CSV. Names, language variants, categories,
and child subdivisions are omitted. The catalog accepts a code only when its
country prefix matches the requested country.

This site or product includes Ipregistry ISO 3166 data available from
<https://ipregistry.co>. The source data is licensed under the
[Creative Commons Attribution-ShareAlike 4.0 International License](https://creativecommons.org/licenses/by-sa/4.0/)
(CC BY-SA 4.0); see the [source license](https://github.com/ipregistry/iso3166/blob/9f8c9fb33b9c9447bf7a1edd7b93a98b7355af61/LICENSE.txt).
The generated code list is a transformed copy of that data and remains subject
to the applicable attribution and share-alike terms.
