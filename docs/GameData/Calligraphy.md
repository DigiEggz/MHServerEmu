# Calligraphy

Calligraphy is a custom game data management system developed by Gazillion. Its main purpose was most likely providing game designers convenient tools for editing game data. Data exported from Calligraphy is stored in the `Calligraphy.sip` archive.

Calligraphy uses five file formats: directory, curve, asset type, blueprint, and prototype. All Calligraphy files start with the same four-byte header:

```csharp
struct CalligraphyHeader
{
    char[3] Magic;
    byte Version;
}
```

The magic string defines what format is used in the file. The version depends on the game version: game versions 1.9-1.17 used Calligraphy version 10, and all later game versions starting with 1.18 released on January 24th 2014 use Calligraphy version 11.

All strings in Calligraphy files are fixed-length UTF-8 strings with the length encoded in a 16-bit value preceding the text:

```csharp
struct FixedString16
{
    ushort StringLength;
    char[StringLength] String;
}
```

## Directory

Directory (`.directory`) files contain information required for the initialization of the `DataDirectory` class. There's a total of five directory files, each containing a number of records with slightly different structures.

All directories start with a Calligraphy header and the number of records contained in the directory.

```csharp
struct DirectoryFile
{
    CalligraphyHeader Header;
    uint NumRecords;
    Record[NumRecords] Records;
}
```

`Curve.directory` (signature `CDR`), `Type.directory` (signature `CDR`), and `Blueprint.directory` (signature `BDR`) have the same standard record structure:

```csharp
struct GenericRecord
{
    ulong Id;
    ulong Guid;
    byte Flags;
    FixedString16 FilePath;
}
```

`Prototype.directory` (signature `PDR`) has a modified structure:

```csharp
struct PrototypeRecord
{
    ulong PrototypeId;
    ulong PrototypeGuid;
    ulong BlueprintId; // Even though it's called BlueprintId, this is actually a parent default prototype id
    byte Flags;
    FixedString16 FilePath;
}
```

`Replacement.directory` (signature `RDR`) is a special directory used for handling deprecated GUIDs. Replacement records are managed by the `ReplacementDirectory` class. This file has a different record structure:

```csharp
ulong OldGuid;
ulong NewGuid;
FixedString16 Name;
```

Please note that file paths contained in these directory files use the `\` symbol as the path delimiter, while [pak files](./PakFile.md) use `/`. To use these paths for reading files from the pak file system you need to replace `\` with `/` while reading them.

## Curve

Curve (`.curve`, signature `CRV`) files contain collections of 64-bit floating point values. They are used for various purposes as values for prototype fields. Loaded curves are managed by the `CurveDirectory` class.

Curve files have the following structure:

```csharp
struct CurveFile
{
    CalligraphyHeader Header;
    int StartPosition;
    int EndPosition;
    double[EndPosition - StartPosition + 1] Values; 
}
```

## Asset Type

Asset type (`.type`, signature `TYP`) files contain collections of asset references of specific types. They function as essentially enumerators for instances of various types of external data, and some of them are bound to enums in code. Loaded asset types are managed by the `AssetDirectory` class.

Asset type files have the following structure:

```csharp
struct AssetTypeFile
{
    CalligraphyHeader Header;
    ushort NumAssets;
    Asset[NumAssets]; Assets;
}
```

Each asset in an asset type has the following structure:

```csharp
struct Asset
{
    ulong AssetId;    // Processed by the client as the StringId for the name
    ulong AssetGuid;
    byte Flags;
    FixedString16 Name; 
}
```

## Blueprint

Blueprint (`.blueprint`, signature `BPT`) files contain definitions for various prototype types. Each blueprint is paired with a default prototype (`.defaults`) that contains default values for all fields defined in the blueprint.

Blueprint files have the following structure:

```csharp
struct BlueprintFile
{
    CalligraphyHeader Header;
    FixedString16 RuntimeBinding;    // Name of the class that handles prototypes that use this blueprint
    ulong DefaultPrototypeId;

    ushort NumParents;
    BlueprintReference[NumParents] Parents;

    ushort NumContributingBlueprints;
    BlueprintReference[NumContributingBlueprints] ContributingBlueprints;

    ushort NumMembers;
    BlueprintMember[NumMembers] Members;
}
```

Blueprint references actually reference the default prototype bound to a blueprint, and not the blueprint itself. They have the following structure:

```csharp
struct BlueprintReference
{
    ulong PrototypeId;
    byte Flags;
}
```

Blueprint members are definitions for prototype fields that have the following structure:

```csharp
struct BlueprintMember
{
    ulong FieldId;        // Processed by the client as a StringId
    FixedString16 FieldName;
    byte ValueType;
    byte ContainerType;

    if (ValueType == Asset || ValueType == Curve
    || ValueType == Prototype || ValueType == RHStruct)
        ulong Subtype;
}
```

`ValueType` defines the type of data stored in a field. Calligraphy supports nine value types:

```csharp
enum CalligraphyValueType : byte
{
    Asset = 0x41,       // A (Id reference to an asset)
    Boolean = 0x42,     // B (Stored as a UInt64)
    Curve = 0x43,       // C (Id reference to a curve)
    Double = 0x44,      // D (For all floating point values)
    Long = 0x4c,        // L (For all integer values)
    Prototype = 0x50,   // P (Id reference to another prototype)
    RHStruct = 0x52,    // R (Embedded prototype without an id)
    String = 0x53,      // S (Id reference to a localized string)
    Type = 0x54         // T (Id reference to an AssetType)
}
```

`ContainerType` defines whether a field contains a single value or a list of multiple values:

```csharp
enum CalligraphyContainerType : byte
{
    Simple = 0x53,      // Simple
    List = 0x4c         // List (only for assets, prototypes, rhstructs, and types)
}
```

`Subtype` specifies the id of the parent value that the value in this field has to inherit from. For example, for prototypes it is the id of the default prototype. Only assets, curves, and prototypes have subtypes.

## Prototype

Prototype (`.prototype` or `.defaults`, signature `PTP`) files contain values for fields defined in blueprints. Each `.defaults` prototype file is paired with a `.blueprint` file that defines its fields, and `.prototype` files inherit from these default prototypes.

Prototype files have the following structure:

```csharp
struct PrototypeFile
{
    CalligraphyHeader Header;
    Prototype Prototype;
}
```

Prototypes themselves have the following structure:

```csharp
struct Prototype
{
    PrototypeDataHeader Header;

    if (Header.DataExists)
    {
        ushort NumFieldGroups;
        PrototypeFieldGroup[NumFieldGroups] FieldGroups;
    } 
}
```

Prototype data header has the following structure:

```csharp
struct PrototypeDataHeader
{
    byte Flags;
    bool ReferenceExists = (Flags & 0x01) > 0;
    bool DataExists = (Flags & 0x02) > 0;
    bool PolymorphicData = (Flags & 0x04) > 0;

    if (ReferenceExists)
        ulong ReferenceType;    // Parent prototype id, invalid (0) for .defaults 
}
```

Each field group is a collection of fields belonging to blueprints that contribute to a prototype. They have the following structure:

```csharp
struct PrototypeFieldGroup
{
    ulong DeclaringBlueprintId;    // .defaults prototype id
    byte BlueprintCopyNumber;

    ushort NumSimpleFields;
    PrototypeSimpleField[NumSimpleFields] SimpleFields;

    ushort NumListFields;
    PrototypeListField[NumListFields] ListFields;
}
```

Simple fields contain a single value and have the following structure:

```csharp
struct PrototypeSimpleField
{
    ulong FieldId;
    ValueType Type;    // See Blueprint section above for more info
    object Value; 
}
```

List fields contain a list of values and have a similar structure:

```csharp
struct PrototypeListField
{
    ulong FieldId;
    ValueType Type;
    ushort NumValues;
    object[ValuesLength] Values;  
}
```

 The actual value type depends on `Type`:

- `Boolean`: a boolean stored as a 64-bit unsigned integer.

- `Double`: a double precision 64-bit floating point value.

- `Long`: a 64-bit signed integer value.

- `RHStruct`: a new prototype definition starting with `PrototypeDataHeader`.

- `Asset`, `Curve`, `Prototype`, `String`, `Type`: a 64-bit data id.

RHStructs are fully-featured prototypes without an id that can have other RHStructs as their field values. Because of that, some prototypes have a heavily nested recursive structure.
