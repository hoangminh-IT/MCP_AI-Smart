export type JsonSchema = Record<string, unknown>;
function object(value: unknown): value is JsonSchema { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
export function schemaProperties(schema: JsonSchema): Record<string, JsonSchema> {
  return object(schema.properties) ? Object.fromEntries(Object.entries(schema.properties).filter((entry): entry is [string, JsonSchema] => object(entry[1]))) : {};
}
export function resolveField(field: JsonSchema, root: JsonSchema, depth = 0): JsonSchema {
  if (depth > 8) return field;
  if (typeof field.$ref === 'string' && field.$ref.startsWith('#/')) {
    let target: unknown = root;
    for (const part of field.$ref.slice(2).split('/')) target = object(target) ? target[part.replace(/~1/g, '/').replace(/~0/g, '~')] : undefined;
    if (object(target)) return resolveField({ ...target, ...Object.fromEntries(Object.entries(field).filter(([key]) => key !== '$ref')) }, root, depth + 1);
  }
  const union = Array.isArray(field.anyOf) ? field.anyOf : Array.isArray(field.oneOf) ? field.oneOf : undefined;
  if (union) {
    const nonNull = union.filter((variant): variant is JsonSchema => object(variant) && variant.type !== 'null');
    if (nonNull.length === 1) return { ...field, ...resolveField(nonNull[0], root, depth + 1), nullable: union.some((variant) => object(variant) && variant.type === 'null') };
  }
  return field;
}
export function fieldType(field: JsonSchema): string | undefined {
  if (typeof field.type === 'string') return field.type;
  if (Array.isArray(field.type)) { const types = field.type.filter((value) => value !== 'null'); if (types.length === 1 && typeof types[0] === 'string') return types[0]; }
  return undefined;
}
export function allowsNull(field: JsonSchema): boolean {
  return field.nullable === true || field.type === 'null' || Array.isArray(field.type) && field.type.includes('null');
}
export function schemaSample(schema: JsonSchema): Record<string, unknown> {
  const required = Array.isArray(schema.required) ? schema.required : [];
  const result: Record<string, unknown> = {};
  for (const [name, raw] of Object.entries(schemaProperties(schema))) {
    const field = resolveField(raw, schema);
    if ('default' in field) { result[name] = field.default; continue; }
    if (!required.includes(name)) continue;
    if (Array.isArray(field.examples) && field.examples.length) { result[name] = field.examples[0]; continue; }
    if (Array.isArray(field.enum) && field.enum.length) { result[name] = field.enum[0]; continue; }
    const type = fieldType(field);
    result[name] = type === 'integer' || type === 'number' ? typeof field.minimum === 'number' ? field.minimum : typeof field.exclusiveMinimum === 'number' ? Math.floor(field.exclusiveMinimum) + 1 : 0
      : type === 'boolean' ? false : type === 'array' ? [] : type === 'object' ? {} : '';
  }
  return result;
}
export function validateArguments(value: unknown, schema: JsonSchema): string[] {
  if (!object(value)) return ['Arguments phải là một JSON object.'];
  const errors: string[] = []; const properties = schemaProperties(schema);
  for (const name of Array.isArray(schema.required) ? schema.required : []) if (typeof name === 'string' && !Object.hasOwn(value, name)) errors.push(`${name}: thiếu tham số bắt buộc.`);
  for (const [name, input] of Object.entries(value)) {
    const raw = Object.hasOwn(properties, name) ? properties[name] : undefined;
    if (!raw) { if (schema.additionalProperties === false) errors.push(`${name}: không có trong schema.`); continue; }
    const field = resolveField(raw, schema); const type = fieldType(field);
    if (input === null) {
      if (!allowsNull(field) && type) errors.push(`${name}: không chấp nhận null.`);
      if (Array.isArray(field.enum) && !field.enum.includes(null)) errors.push(`${name}: giá trị không nằm trong enum.`);
      continue;
    }
    const matches = type === 'integer' ? typeof input === 'number' && Number.isSafeInteger(input) : type === 'number' ? typeof input === 'number' && Number.isFinite(input)
      : type === 'string' ? typeof input === 'string' : type === 'boolean' ? typeof input === 'boolean' : type === 'array' ? Array.isArray(input) : type === 'object' ? object(input) : true;
    if (!matches) { errors.push(`${name}: cần kiểu ${type}.`); continue; }
    if (Array.isArray(field.enum) && !field.enum.some((option) => JSON.stringify(option) === JSON.stringify(input))) errors.push(`${name}: giá trị không nằm trong enum.`);
    if (typeof input === 'number') {
      if (typeof field.minimum === 'number' && input < field.minimum) errors.push(`${name}: tối thiểu ${field.minimum}.`);
      if (typeof field.maximum === 'number' && input > field.maximum) errors.push(`${name}: tối đa ${field.maximum}.`);
      if (typeof field.exclusiveMinimum === 'number' && input <= field.exclusiveMinimum) errors.push(`${name}: phải lớn hơn ${field.exclusiveMinimum}.`);
      if (typeof field.exclusiveMaximum === 'number' && input >= field.exclusiveMaximum) errors.push(`${name}: phải nhỏ hơn ${field.exclusiveMaximum}.`);
    }
    if (typeof input === 'string') {
      if (typeof field.minLength === 'number' && input.length < field.minLength) errors.push(`${name}: ít nhất ${field.minLength} ký tự.`);
      if (typeof field.maxLength === 'number' && input.length > field.maxLength) errors.push(`${name}: tối đa ${field.maxLength} ký tự.`);
    }
  }
  return errors;
}
