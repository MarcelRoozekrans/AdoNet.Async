/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docs: [
    'intro',
    {
      type: 'category',
      label: 'Getting Started',
      items: [
        'getting-started/installation',
        'getting-started/quick-start',
      ],
    },
    {
      type: 'category',
      label: 'Core Concepts',
      items: [
        'core-concepts/async-interfaces',
        'core-concepts/await-foreach',
        'core-concepts/async-datatable',
        'core-concepts/async-events',
        'core-concepts/sync-bridge',
      ],
    },
    {
      type: 'category',
      label: 'Typed DataSets',
      items: [
        'typed-datasets/overview',
        'typed-datasets/generating-from-xsd',
        'typed-datasets/typed-access',
        'typed-datasets/relations',
        'typed-datasets/annotations-reference',
      ],
    },
    {
      type: 'category',
      label: 'Serialization',
      items: [
        'serialization/newtonsoft-json',
        'serialization/system-text-json',
      ],
    },
    {
      type: 'category',
      label: 'Adapters & DI',
      items: [
        'adapters-di/wrapping-providers',
        'adapters-di/dependency-injection',
      ],
    },
    {
      type: 'category',
      label: 'Cookbook',
      items: [
        'cookbook/migrate-existing-code',
        'cookbook/fill-typed-dataset',
        'cookbook/async-validation-events',
        'cookbook/json-api-typed-datasets',
      ],
    },
    'api-reference/api-reference',
    'performance/performance',
  ],
};

export default sidebars;
