#!/usr/bin/env python
""" This is a hacked version of PyUnit that extends its reporting capabilities
with optional meta data on the test cases.  It also makes it possible to
separate the standard and error output streams in TextTestRunner.

It's a hack rather than a set of subclasses because a) Steve had used double
underscore private attributes for some things I needed access to, and b) the
changes affected so many classes that it was easier just to hack it.

The changes are in the following places:
TestCase:
   - minor refactoring of  __init__ and __call__ internals
   - added some attributes and methods for storing and retrieving meta data

_TextTestResult
   - refactored the stream handling
   - incorporated all the output code from TextTestRunner
   - made the output of FAIL and ERROR information more flexible and
     incorporated the new meta data from TestCase
   - added a flag called 'explain' to __init__ that controls whether the new '
     explanation'   meta data from TestCase is printed along with tracebacks
   
TextTestRunner
   - delegated all output to _TextTestResult
   - added 'err' and 'explain' to the __init__ signature to match the changes
     in _TextTestResult
   
TestProgram
   - added -e and --explain as flags on the command line

-- Tavis Rudd <tavis@redonions.net> (Sept 28th, 2001)

- _TestTextResult.printErrorList(): print blank line after each traceback

-- Mike Orr <mso@oz.net> (Nov 11, 2002)

TestCase methods copied from unittest in Python 2.3:
  - .assertAlmostEqual(first, second, places=7, msg=None): to N decimal places.
  - .failIfAlmostEqual(first, second, places=7, msg=None)

-- Mike Orr (Jan 5, 2004)


Below is the original docstring for unittest.
---------------------------------------------------------------------------
Python unit testing framework, based on Erich Gamma's JUnit and Kent Beck's
Smalltalk testing framework.

This module contains the core framework classes that form the basis of
specific test cases and suites (TestCase, TestSuite etc.), and also a
text-based utility class for running the tests and reporting the results
(TextTestRunner).

Simple usage:

    import unittest

    class IntegerArithmenticTestCase(unittest.TestCase):
        def testAdd(self):  ## test method names begin 'test*'
            self.assertEquals((1 + 2), 3)
            self.assertEquals(0 + 1, 1)
        def testMultiply(self);
            self.assertEquals((0 * 10), 0)
            self.assertEquals((5 * 8), 40)

    if __name__ == '__main__':
        unittest.main()

Further information is available in the bundled documentation, and from

  http://pyunit.sourceforge.net/

Copyright (c) 1999, 2000, 2001 Steve Purcell
This module is free software, and you may redistribute it and/or modify
it under the same terms as Python itself, so long as this copyright message
and disclaimer are retained in their original form.

IN NO EVENT SHALL THE AUTHOR BE LIABLE TO ANY PARTY FOR DIRECT, INDIRECT,
SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES ARISING OUT OF THE USE OF
THIS CODE, EVEN IF THE AUTHOR HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH
DAMAGE.

THE AUTHOR SPECIFICALLY DISCLAIMS ANY WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
PARTICULAR PURPOSE.  THE CODE PROVIDED HEREUNDER IS ON AN "AS IS" BASIS,
AND THERE IS NO OBLIGATION WHATSOEVER TO PROVIDE MAINTENANCE,
SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
"""

__author__ = "Steve Purcell"
__email__ = "stephen_purcell at yahoo dot com"
__revision__ = "$Revision: 1.11 $"[11:-2]


##################################################
## DEPENDENCIES ##

import os
import re
import string
import sys
import time
import traceback
import types
import pprint

##################################################
## CONSTANTS & GLOBALS

try:
    True,False
except NameError:
    True, False = (1==1),(1==0)

##############################################################################
# Test framework core
##############################################################################


class TestResult:
    """Holder for test result information.

    Test results are automatically managed by the TestCase and TestSuite
    classes, and do not need to be explicitly manipulated by writers of tests.

    Each instance holds the total number of tests run, and collections of
    failures and errors that occurred among those test runs. The collections
    contain tuples of (testcase, exceptioninfo), where exceptioninfo is a
    tuple of values as returned by sys.exc_info().
    """
    def __init__(self):
        self.failures = []
        self.errors = []
        self.testsRun = 0
        self.shouldStop = 0

    def startTest(self, test):
        "Called when the given test is about to be run"
        self.testsRun = self.testsRun + 1

    def stopTest(self, test):
        "Called when the given test has been run"
        pass

    def addError(self, test, err):
        "Called when an error has occurred"
        self.errors.append((test, err))

    def addFailure(self, test, err):
        "Called when a failure has occurred"
        self.failures.append((test, err))

    def addSuccess(self, test):
        "Called when a test has completed successfully"
        pass

    def wasSuccessful(self):
        "Tells whether or not this result was a success"
        return len(self.failures) == len(self.errors) == 0

    def stop(self):
        "Indicates that the tests should be aborted"
        self.shouldStop = 1

    def __repr__(self):
        return "<%s run=%i errors=%i failures=%i>" % \
               (self.__class__, self.testsRun, len(self.errors),
                len(self.failures))
        
class TestCase:
    """A class whose instances are single test cases.

    By default, the test code itself should be placed in a method named
    'runTest'.

    If the fixture may be used for many test cases, create as
    many test methods as are needed. When instantiating such a TestCase
    subclass, specify in the constructor arguments the name of the test method
    that the instance is to execute.

    Test authors should subclass TestCase for their own tests. Construction
    and deconstruction of the test's environment ('fixture') can be
    implemented by overriding the 'setUp' and 'tearDown' methods respectively.

    If it is necessary to override the __init__ method, the base class
    __init__ method must always be called. It is important that subclasses
    should not change the signature of their __init__ method, since instances
    of the classes are instantiated automatically by parts of the framework
    in order to be run.
    """

    # This attribute determines which exception will be raised when
    # the instance's assertion methods fail; test methods raising this
    # exception will be deemed to have 'failed' rather than 'errored'

    failureException = AssertionError

    # the name of the fixture.  Used for displaying meta data about the test
    name = None
    
    def __init__(self, methodName='runTest'):
        """Create an instance of the class that will use the named test
        method when executed. Raises a ValueError if the instance does
        not have a method with the specified name.
        """
        self._testMethodName = methodName
        self._setupTestMethod()
        self._setupMetaData()

    def _setupTestMethod(self):
        try:
            self._testMethod = getattr(self, self._testMethodName)
        except AttributeError:
            raise ValueError, "no such test method in %s: %s" % \
                  (self.__class__, self._testMethodName)
        
    ## meta data methods
        
    def _setupMetaData(self):
        """Setup the default meta data for the test case:

        - id: self.__class__.__name__ + testMethodName OR self.name + testMethodName
        - description: 1st line of Class docstring + 1st line of method docstring
        - explanation: rest of Class docstring + rest of method docstring
        
        """

        
        testDoc = self._testMethod.__doc__ or '\n'
        testDocLines = testDoc.splitlines()
        
        testDescription = testDocLines[0].strip() 
        if len(testDocLines) > 1:
            testExplanation = '\n'.join(
                [ln.strip() for ln in testDocLines[1:]]
                ).strip()
        else:
            testExplanation = ''
            
        fixtureDoc = self.__doc__ or '\n'
        fixtureDocLines = fixtureDoc.splitlines()
        fixtureDescription = fixtureDocLines[0].strip()
        if len(fixtureDocLines) > 1:
            fixtureExplanation = '\n'.join(
                [ln.strip() for ln in fixtureDocLines[1:]]
                ).strip()
        else:
            fixtureExplanation = ''
        
        if not self.name:
            self.name = self.__class__
        self._id = "%s.%s" % (self.name, self._testMethodName)
        
        if not fixtureDescription:
            self._description = testDescription
        else:
            self._description = fixtureDescription + ', ' + testDescription

        if not fixtureExplanation:
            self._explanation = testExplanation
        else:
            self._explanation = ['Fixture Explanation:',
                                 '--------------------',
                                 fixtureExplanation,
                                 '',
                                 'Test Explanation:',
                                 '-----------------',
                                 testExplanation
                                 ]
            self._explanation = '\n'.join(self._explanation)

    def id(self):
        return self._id

    def setId(self, id):
        self._id = id

    def describe(self):
        """Returns a one-line description of the test, or None if no
        description has been provided.

        The default implementation of this method returns the first line of
        the specified test method's docstring.
        """
        return self._description

    shortDescription = describe
    
    def setDescription(self, descr):
        self._description = descr
    
    def explain(self):
        return self._explanation

    def setExplanation(self, expln):
        self._explanation = expln

    ## core methods

    def setUp(self):
        "Hook method for setting up the test fixture before exercising it."
        pass
    
    def run(self, result=None):
        return self(result)
        
    def tearDown(self):
        "Hook method for deconstructing the test fixture after testing it."
        pass

    def debug(self):
        """Run the test without collecting errors in a TestResult"""
        self.setUp()
        self._testMethod()
        self.tearDown()

    ## internal methods

    def defaultTestResult(self):
        return TestResult()
    
    def __call__(self, result=None):
        if result is None:
            result = self.defaultTestResult()
        
        result.startTest(self)
        try:
            try:
                self.setUp()
            except:
                result.addError(self, self.__exc_info())
                return
            
            ok = 0
            try:
                self._testMethod()
                ok = 1
            except self.failureException, e:
                result.addFailure(self, self.__exc_info())
            except:
                result.addError(self, self.__exc_info())
            try:
                self.tearDown()
            except:
                result.addError(self, self.__exc_info())
                ok = 0
            if ok:
                result.addSuccess(self)
        finally:
            result.stopTest(self)
            
        return result
        
    def countTestCases(self):
        return 1
       
    def __str__(self):
        return "%s (%s)" % (self._testMethodName, self.__class__)

    def __repr__(self):
        return "<%s testMethod=%s>" % \
               (self.__class__, self._testMethodName)

    def __exc_info(self):
        """Return a version of sys.exc_info() with the traceback frame
           minimised; usually the top level of the traceback frame is not
           needed.
        """
        exctype, excvalue, tb = sys.exc_info()
        if sys.platform[:4] == 'java': ## tracebacks look different in Jython
            return (exctype, excvalue, tb)
        newtb = tb.tb_next
        if newtb is None:
            return (exctype, excvalue, tb)
        return (exctype, excvalue, newtb)

    ## methods for use by the test cases

    def fail(self, msg=None):
        """Fail immediately, with the given message."""
        raise self.failureException, msg

    def failIf(self, expr, msg=None):
        "Fail the test if the expression is true."
        if expr: raise self.failureException, msg

    def failUnless(self, expr, msg=None):
        """Fail the test unless the expression is true."""
        if not expr: raise self.failureException, msg

    def failUnlessRaises(self, excClass, callableObj, *args, **kwargs):
        """Fail unless an exception of class excClass is thrown
           by callableObj when invoked with arguments args and keyword
           arguments kwargs. If a different type of exception is
           thrown, it will not be caught, and the test case will be
           deemed to have suffered an error, exactly as for an
           unexpected exception.
        """
        try:
            apply(callableObj, args, kwargs)
        except excClass:
            return
        else:
            if hasattr(excClass,'__name__'): excName = excClass.__name__
            else: excName = str(excClass)
            raise self.failureException, excName

    def failUnlessEqual(self, first, second, msg=None):
        """Fail if the two objects are unequal as determined by the '!='
           operator.
        """
        if first != second:
            raise self.failureException, (msg or '%s != %s' % (first, second))

    def failIfEqual(self, first, second, msg=None):
        """Fail if the two objects are equal as determined by the '=='
           operator.
        """
        if first == second:
            raise self.failureException, (msg or '%s == %s' % (first, second))

    def failUnlessAlmostEqual(self, first, second, places=7, msg=None):
        """Fail if the two objects are unequal as determined by their
           difference rounded to the given number of decimal places
           (default 7) and comparing to zero.

           Note that decimal places (from zero) is usually not the same
           as significant digits (measured from the most signficant digit).
        """
        if round(second-first, places) != 0:
            raise self.failureException, \
                  (msg or '%s != %s within %s places' % (`first`, `second`, `places` ))

    def failIfAlmostEqual(self, first, second, places=7, msg=None):
        """Fail if the two objects are equal as determined by their
           difference rounded to the given number of decimal places
           (default 7) and comparing to zero.

           Note that decimal places (from zero) is usually not the same
           as significant digits (measured from the most signficant digit).
        """
        if round(second-first, places) == 0:
            raise self.failureException, \
                  (msg or '%s == %s within %s places' % (`first`, `second`, `places`))

    ## aliases

    assertEqual = assertEquals = failUnlessEqual

    assertNotEqual = assertNotEquals = failIfEqual

    assertAlmostEqual = assertAlmostEquals = failUnlessAlmostEqual

    assertNotAlmostEqual = assertNotAlmostEquals = failIfAlmostEqual

    assertRaises = failUnlessRaises

    assert_ = failUnless


class FunctionTestCase(TestCase):
    """A test case that wraps a test function.

    This is useful for slipping pre-existing test functions into the
    PyUnit framework. Optionally, set-up and tidy-up functions can be
    supplied. As with TestCase, the tidy-up ('tearDown') function will
    always be called if the set-up ('setUp') function ran successfully.
    """

    def __init__(self, testFunc, setUp=None, tearDown=None,
                 description=None):
        TestCase.__init__(self)
        self.__setUpFunc = setUp
        self.__tearDownFunc = tearDown
        self.__testFunc = testFunc
        self.__description = description

    def setUp(self):
        if self.__setUpFunc is not None:
            self.__setUpFunc()

    def tearDown(self):
        if self.__tearDownFunc is not None:
            self.__tearDownFunc()

    def runTest(self):
        self.__testFunc()

    def id(self):
        return self.__testFunc.__name__

    def __str__(self):
        return "%s (%s)" % (self.__class__, self.__testFunc.__name__)

    def __repr__(self):
        return "<%s testFunc=%s>" % (self.__class__, self.__testFunc)


    def describe(self):
        if self.__description is not None: return self.__description
        doc = self.__testFunc.__doc__
        return doc and string.strip(string.split(doc, "\n")[0]) or None
    
    ## aliases
    shortDescription = describe

class TestSuite:
    """A test suite is a composite test consisting of a number of TestCases.

    For use, create an instance of TestSuite, then add test case instances.
    When all tests have been added, the suite can be passed to a test
    runner, such as TextTestRunner. It will run the individual test cases
    in the order in which they were added, aggregating the results. When
    subclassing, do not forget to call the base class constructor.
    """
    def __init__(self, tests=(), suiteName=None):
        self._tests = []
        self._testMap = {}
        self.suiteName = suiteName
        self.addTests(tests)

    def __repr__(self):
        return "<%s tests=%s>" % (self.__class__, pprint.pformat(self._tests))

    __str__ = __repr__

    def countTestCases(self):
        cases = 0
        for test in self._tests:
            cases = cases + test.countTestCases()
        return cases

    def addTest(self, test):
        self._tests.append(test)
        if isinstance(test, TestSuite) and test.suiteName:
            name = test.suiteName
        elif isinstance(test, TestCase):
            #print test, test._testMethodName
            name = test._testMethodName
        else:
            name = test.__class__.__name__
        self._testMap[name] = test
        
    def addTests(self, tests):
        for test in tests:
            self.addTest(test)

    def getTestForName(self, name):
        return self._testMap[name]

    def run(self, result):
        return self(result)

    def __call__(self, result):
        for test in self._tests:
            if result.shouldStop:
                break
            test(result)
        return result

    def debug(self):
        """Run the tests without collecting errors in a TestResult"""
        for test in self._tests: test.debug()


##############################################################################
# Text UI
##############################################################################

class StreamWrapper:
    def __init__(self, out=sys.stdout, err=sys.stderr):
        self._streamOut = out
        self._streamErr = err

    def write(self, txt):
        self._streamOut.write(txt)
        self._streamOut.flush()
    
    def writeln(self, *lines):
        for line in lines:
            self.write(line + '\n')
        if not lines:
            self.write('\n')

    def writeErr(self, txt):
        self._streamErr.write(txt)
    
    def writelnErr(self, *lines):
        for line in lines:
            self.writeErr(line + '\n')
        if not lines:
            self.writeErr('\n')


class _TextTestResult(TestResult, StreamWrapper):
    _separatorWidth = 70
    _sep1 = '='
    _sep2 = '-'
    _errorSep1 = '*'
    _errorSep2 = '-'
    _errorSep3 = ''
    
    def __init__(self,
                 stream=sys.stdout,
                 errStream=sys.stderr,
                 verbosity=1,
                 explain=False):
        
        TestResult.__init__(self)
        StreamWrapper.__init__(self, out=stream, err=errStream)        

        self._verbosity = verbosity
        self._showAll = verbosity > 1
        self._dots = (verbosity == 1)
        self._explain = explain

    ## startup and shutdown methods
        
    def beginTests(self):
        self._startTime = time.time()

    def endTests(self):
        self._stopTime = time.time()
        self._timeTaken = float(self._stopTime - self._startTime)

    def stop(self):
        self.shouldStop = 1
        
    ## methods called for each test
        
    def startTest(self, test):
        TestResult.startTest(self, test)
        if self._showAll:
            self.write("%s (%s)" %( test.id(), test.describe() ) )
            self.write(" ... ")

    def addSuccess(self, test):
        TestResult.addSuccess(self, test)
        if self._showAll:
            self.writeln("ok")
        elif self._dots:
            self.write('.')

    def addError(self, test, err):
        TestResult.addError(self, test, err)
        if self._showAll:
            self.writeln("ERROR")
        elif self._dots:
            self.write('E')
        if err[0] is KeyboardInterrupt:
            self.stop()

    def addFailure(self, test, err):
        TestResult.addFailure(self, test, err)
        if self._showAll:
            self.writeln("FAIL")
        elif self._dots:
            self.write('F')

    ## display methods

    def summarize(self):
        self.printErrors()
        self.writeSep2()
        run = self.testsRun
        self.writeln("Ran %d test%s in %.3fs" %
                            (run, run == 1 and "" or "s", self._timeTaken))
        self.writeln()
        if not self.wasSuccessful():
            self.writeErr("FAILED (")
            failed, errored = map(len, (self.failures, self.errors))
            if failed:
                self.writeErr("failures=%d" % failed)
            if errored:
                if failed: self.writeErr(", ")
                self.writeErr("errors=%d" % errored)
            self.writelnErr(")")
        else:
            self.writelnErr("OK")

    def writeSep1(self):
        self.writeln(self._sep1 * self._separatorWidth)

    def writeSep2(self):
        self.writeln(self._sep2 * self._separatorWidth)

    def writeErrSep1(self):
        self.writeln(self._errorSep1 * self._separatorWidth)

    def writeErrSep2(self):
        self.writeln(self._errorSep2 * self._separatorWidth)

    def printErrors(self):
        if self._dots or self._showAll:
            self.writeln()
        self.printErrorList('ERROR', self.errors)
        self.printErrorList('FAIL', self.failures)

    def printErrorList(self, flavour, errors):
        for test, err in errors:
            self.writeErrSep1()
            self.writelnErr("%s %s (%s)" % (flavour, test.id(), test.describe() ))
            if self._explain:
                expln = test.explain()
                if expln:
                    self.writeErrSep2()
                    self.writeErr( expln )
                    self.writelnErr()

            self.writeErrSep2()
            for line in apply(traceback.format_exception, err):
                for l in line.split("\n")[:-1]:
                    self.writelnErr(l)
            self.writelnErr("")

class TextTestRunner:
    def __init__(self, 
                 stream=sys.stdout,
                 errStream=sys.stderr,
                 verbosity=1,
                 explain=False):

        self._out = stream
        self._err = errStream
        self._verbosity = verbosity
        self._explain = explain
        
    ## main methods

    def run(self, test):
        result = self._makeResult()
        result.beginTests()
        test( result )
        result.endTests()       
        result.summarize()
        
        return result
    
    ## internal methods

    def _makeResult(self):
        return _TextTestResult(stream=self._out,
                               errStream=self._err,
                               verbosity=self._verbosity,
                               explain=self._explain,
                               )

##############################################################################
# Locating and loading tests
##############################################################################

class TestLoader:
    """This class is responsible for loading tests according to various
    criteria and returning them wrapped in a Test
    """
    testMethodPrefix = 'test'
    sortTestMethodsUsing = cmp
    suiteClass = TestSuite

    def loadTestsFromTestCase(self, testCaseClass):
        """Return a suite of all tests cases contained in testCaseClass"""
        return self.suiteClass(tests=map(testCaseClass,
                                         self.getTestCaseNames(testCaseClass)),
                               suiteName=testCaseClass.__name__)

    def loadTestsFromModule(self, module):
        """Return a suite of all tests cases contained in the given module"""
        tests = []
        for name in dir(module):
            obj = getattr(module, name)
            if type(obj) == types.ClassType and issubclass(obj, TestCase):
                tests.append(self.loadTestsFromTestCase(obj))
        return self.suiteClass(tests)

    def loadTestsFromName(self, name, module=None):
        """Return a suite of all tests cases given a string specifier.

        The name may resolve either to a module, a test case class, a
        test method within a test case class, or a callable object which
        returns a TestCase or TestSuite instance.

        The method optionally resolves the names relative to a given module.
        """
        parts = string.split(name, '.')
        if module is None:
            if not parts:
                raise ValueError, "incomplete test name: %s" % name
            else:
                parts_copy = parts[:]
                while parts_copy:
                    try:
                        module = __import__(string.join(parts_copy,'.'))
                        break
                    except ImportError:
                        del parts_copy[-1]
                        if not parts_copy: raise
                parts = parts[1:]
        obj = module
        for part in parts:
            if isinstance(obj, TestSuite):
                obj = obj.getTestForName(part)
            else:
                obj = getattr(obj, part)

        if type(obj) == types.ModuleType:
            return self.loadTestsFromModule(obj)
        elif type(obj) == types.ClassType and issubclass(obj, TestCase):
            return self.loadTestsFromTestCase(obj)
        elif type(obj) == types.UnboundMethodType:
            return obj.im_class(obj.__name__)
        elif isinstance(obj, TestSuite):
            return obj
        elif isinstance(obj, TestCase):
            return obj
        elif callable(obj):
            test = obj()
            if not isinstance(test, TestCase) and \
               not isinstance(test, TestSuite):
                raise ValueError, \
                      "calling %s returned %s, not a test" %(obj,test)
            return test
        else:
            raise ValueError, "don't know how to make test from: %s" % obj

    def loadTestsFromNames(self, names, module=None):
        """Return a suite of all tests cases found using the given sequence
        of string specifiers. See 'loadTestsFromName()'.
        """
        suites = []
        for name in names:
            suites.append(self.loadTestsFromName(name, module))
        return self.suiteClass(suites)

    def getTestCaseNames(self, testCaseClass):
        """Return a sorted sequence of method names found within testCaseClass.
        """
        testFnNames = filter(lambda n,p=self.testMethodPrefix: n[:len(p)] == p,
                             dir(testCaseClass))
        for baseclass in testCaseClass.__bases__:
            for testFnName in self.getTestCaseNames(baseclass):
                if testFnName not in testFnNames:  # handle overridden methods
                    testFnNames.append(testFnName)
        if self.sortTestMethodsUsing:
            testFnNames.sort(self.sortTestMethodsUsing)
        return testFnNames



defaultTestLoader = TestLoader()


##############################################################################
# Patches for old functions: these functions should be considered obsolete
##############################################################################

def _makeLoader(prefix, sortUsing, suiteClass=None):
    loader = TestLoader()
    loader.sortTestMethodsUsing = sortUsing
    loader.testMethodPrefix = prefix
    if suiteClass: loader.suiteClass = suiteClass
    return loader

def getTestCaseNames(testCaseClass, prefix, sortUsing=cmp):
    return _makeLoader(prefix, sortUsing).getTestCaseNames(testCaseClass)

def makeSuite(testCaseClass, prefix='test', sortUsing=cmp, suiteClass=TestSuite):
    return _makeLoader(prefix, sortUsing, suiteClass).loadTestsFromTestCase(testCaseClass)

def findTestCases(module, prefix='test', sortUsing=cmp, suiteClass=TestSuite):
    return _makeLoader(prefix, sortUsing, suiteClass).loadTestsFromModule(module)

##############################################################################
# Facilities for running tests from the command line
##############################################################################

class TestProgram:
    """A command-line program that runs a set of tests; this is primarily
       for making test modules conveniently executable.
    """
    USAGE = """\
Usage: %(progName)s [options] [test] [...]

Options:
  -h, --help       Show this message
  -v, --verbose    Verbose output
  -q, --quiet      Minimal output
  -e, --expain     Output extra test details if there is a failure or error
  
Examples:
  %(progName)s                               - run default set of tests
  %(progName)s MyTestSuite                   - run suite 'MyTestSuite'
  %(progName)s MyTestSuite.MyTestCase        - run suite 'MyTestSuite'
  %(progName)s MyTestCase.testSomething      - run MyTestCase.testSomething
  %(progName)s MyTestCase                    - run all 'test*' test methods
                                               in MyTestCase
"""
    def __init__(self, module='__main__', defaultTest=None,
                 argv=None, testRunner=None, testLoader=defaultTestLoader,
                 testSuite=None):
        if type(module) == type(''):
            self.module = __import__(module)
            for part in string.split(module,'.')[1:]:
                self.module = getattr(self.module, part)
        else:
            self.module = module
        if argv is None:
            argv = sys.argv
        self.test = testSuite
        self.verbosity = 1
        self.explain = 0
        self.defaultTest = defaultTest
        self.testRunner = testRunner
        self.testLoader = testLoader
        self.progName = os.path.basename(argv[0])
        self.parseArgs(argv)
        self.runTests()

    def usageExit(self, msg=None):
        if msg: print msg
        print self.USAGE % self.__dict__
        sys.exit(2)

    def parseArgs(self, argv):
        import getopt
        try:
            options, args = getopt.getopt(argv[1:], 'hHvqer',
                                  ['help','verbose','quiet','explain', 'raise'])
            for opt, value in options:
                if opt in ('-h','-H','--help'):
                    self.usageExit()
                if opt in ('-q','--quiet'):
                    self.verbosity = 0
                if opt in ('-v','--verbose'):
                    self.verbosity = 2
                if opt in ('-e','--explain'):
                    self.explain = True
            if len(args) == 0 and self.defaultTest is None and self.test is None:
                self.test = self.testLoader.loadTestsFromModule(self.module)
                return
            if len(args) > 0:
                self.testNames = args
            else:
                self.testNames = (self.defaultTest,)
            self.createTests()
        except getopt.error, msg:
            self.usageExit(msg)

    def createTests(self):
        if self.test == None:
            self.test = self.testLoader.loadTestsFromNames(self.testNames,
                                                           self.module)

    def runTests(self):
        if self.testRunner is None:
            self.testRunner = TextTestRunner(verbosity=self.verbosity,
                                             explain=self.explain)
        result = self.testRunner.run(self.test)
        self._cleanupAfterRunningTests()
        sys.exit(not result.wasSuccessful())

    def _cleanupAfterRunningTests(self):
        """A hook method that is called immediately prior to calling
        sys.exit(not result.wasSuccessful()) in self.runTests().
        """
        pass

main = TestProgram


##############################################################################
# Executing this module from the command line
##############################################################################

if __name__ == "__main__":
    main(module=None)

# vim: shiftwidth=4 tabstop=4 expandtab
