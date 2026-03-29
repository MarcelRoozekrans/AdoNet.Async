import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import styles from './index.module.css';

function HeroBanner() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero hero--primary', styles.heroBanner)}>
      <div className="container">
        <img src="img/logo.svg" alt="AdoNet.Async" className={styles.heroLogo} />
        <h1 className="hero__title">{siteConfig.title}</h1>
        <p className="hero__subtitle">{siteConfig.tagline}</p>
        <div className={styles.buttons}>
          <Link className="button button--secondary button--lg" to="/docs/getting-started/quick-start">
            Get Started
          </Link>
          <Link className="button button--outline button--lg" to="/docs/intro" style={{marginLeft: '1rem', color: 'white', borderColor: 'white'}}>
            Learn More
          </Link>
        </div>
      </div>
    </header>
  );
}

const features = [
  {
    title: 'Async-First',
    description: 'ValueTask everywhere, IAsyncEnumerable iteration, async events. Built for modern .NET with zero compromises on performance.',
  },
  {
    title: 'Drop-In Replacement',
    description: 'Wrap any existing DbConnection with .AsAsync() and get a fully async interface. No provider-specific code needed.',
  },
  {
    title: 'Typed DataSets',
    description: 'Source generator reads .xsd files and produces strongly-typed async DataSet classes. Full IntelliSense, compile-time safety.',
  },
  {
    title: 'Zero-Alloc Events',
    description: '9 async events on AsyncDataTable backed by ZeroAlloc.AsyncEvents. Sequential dispatch, cancellation support, no allocations.',
  },
  {
    title: 'JSON Serialization',
    description: 'Newtonsoft.Json and System.Text.Json converters with identical wire format. Cross-library compatible.',
  },
  {
    title: 'DI Ready',
    description: 'One-liner registration with AddAsyncData(). Inject IAsyncDbProviderFactory anywhere in your application.',
  },
];

function Feature({title, description}) {
  return (
    <div className={clsx('col col--4')}>
      <div className="padding-horiz--md padding-vert--lg">
        <h3>{title}</h3>
        <p>{description}</p>
      </div>
    </div>
  );
}

function FeaturesSection() {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {features.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}

function CodeExample() {
  return (
    <section className={styles.codeExample}>
      <div className="container">
        <div className="row">
          <div className="col col--6">
            <h2>Before</h2>
            <pre className={styles.codeBlock}>
{`using var conn = new SqlConnection(cs);
conn.Open();
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT * FROM Users";
var reader = cmd.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine(reader.GetString(1));
}`}
            </pre>
          </div>
          <div className="col col--6">
            <h2>After</h2>
            <pre className={styles.codeBlock}>
{`var conn = new SqlConnection(cs).AsAsync();
await conn.OpenAsync();
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT * FROM Users";
var reader = await cmd.ExecuteReaderAsync();
await foreach (var record in reader)
{
    Console.WriteLine(record.GetString(1));
}`}
            </pre>
          </div>
        </div>
      </div>
    </section>
  );
}

function PackagesSection() {
  return (
    <section className={styles.packages}>
      <div className="container">
        <h2 style={{textAlign: 'center', marginBottom: '2rem'}}>6 Packages, One Ecosystem</h2>
        <div className="row">
          <div className="col col--4">
            <div className={styles.packageCard}>
              <h3>AdoNet.Async</h3>
              <p>Core interfaces and abstract base classes. Zero dependencies.</p>
            </div>
          </div>
          <div className="col col--4">
            <div className={styles.packageCard}>
              <h3>AdoNet.Async.DataSet</h3>
              <p>AsyncDataTable, AsyncDataRow, 9 async events.</p>
            </div>
          </div>
          <div className="col col--4">
            <div className={styles.packageCard}>
              <h3>AdoNet.Async.Adapters</h3>
              <p>Wrap any DbConnection with .AsAsync() + DI support.</p>
            </div>
          </div>
          <div className="col col--4">
            <div className={styles.packageCard}>
              <h3>AdoNet.Async.DataSet.Generator</h3>
              <p>Roslyn source generator for typed DataSets from .xsd.</p>
            </div>
          </div>
          <div className="col col--4">
            <div className={styles.packageCard}>
              <h3>Serialization.NewtonsoftJson</h3>
              <p>Json.NET converters, wire-compatible format.</p>
            </div>
          </div>
          <div className="col col--4">
            <div className={styles.packageCard}>
              <h3>Serialization.SystemTextJson</h3>
              <p>System.Text.Json converters, identical wire format.</p>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

export default function Home() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout
      title="Async-first ADO.NET"
      description="Async-first interfaces and base classes for ADO.NET. Drop-in replacement with ValueTask, IAsyncEnumerable, async events, and typed DataSet generation.">
      <HeroBanner />
      <main>
        <FeaturesSection />
        <CodeExample />
        <PackagesSection />
      </main>
    </Layout>
  );
}
