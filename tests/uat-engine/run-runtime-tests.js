const { DependencyGraph } = require('./dist/src/runtime/dependencies/DependencyGraph');

function runRuntimeTests() {
  console.log('Running Runtime tests...');
  const graph = new DependencyGraph();
  
  // Mock scenarios for the map
  const scenarios = new Map();
  scenarios.set('C', { metadata: { dependencies: ['B'] } });
  scenarios.set('A', { metadata: { dependencies: [] } });
  scenarios.set('B', { metadata: { dependencies: ['A'] } });

  const order = graph.buildExecutionOrder(scenarios);
  if (order.join(',') !== 'A,B,C') {
    throw new Error(`Topological sort failed. Expected A,B,C but got ${order.join(',')}`);
  }

  // Circular dep
  const circ = new Map();
  circ.set('A', { metadata: { dependencies: ['B'] } });
  circ.set('B', { metadata: { dependencies: ['A'] } });
  try {
    graph.buildExecutionOrder(circ);
    throw new Error('Failed to detect circular dependency');
  } catch (e) {
    if (e.message.includes('Failed to detect circular dependency')) throw e;
  }

  console.log('Runtime tests passed successfully!');
}

runRuntimeTests();
