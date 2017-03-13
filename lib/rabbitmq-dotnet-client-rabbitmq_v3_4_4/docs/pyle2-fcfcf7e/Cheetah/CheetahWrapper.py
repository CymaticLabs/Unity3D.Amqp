#!/usr/bin/env python
# $Id: CheetahWrapper.py,v 1.25 2006/02/04 00:59:46 tavis_rudd Exp $
"""Cheetah command-line interface.

2002-09-03 MSO: Total rewrite.
2002-09-04 MSO: Bugfix, compile command was using wrong output ext.
2002-11-08 MSO: Another rewrite.

Meta-Data
================================================================================
Author: Tavis Rudd <tavis@damnsimple.com> and Mike Orr <iron@mso.oz.net>
Version: $Revision: 1.25 $
Start Date: 2001/03/30
Last Revision Date: $Date: 2006/02/04 00:59:46 $
"""
__author__ = "Tavis Rudd <tavis@damnsimple.com> and Mike Orr <iron@mso.oz.net>"
__revision__ = "$Revision: 1.25 $"[11:-2]

import getopt, glob, os, pprint, re, shutil, sys
import cPickle as pickle

from Cheetah.Version import Version
from Cheetah.Template import Template, DEFAULT_COMPILER_SETTINGS
from Cheetah.Utils.Misc import mkdirsWithPyInitFiles
from Cheetah.Utils.optik import OptionParser

optionDashesRE = re.compile(  R"^-{1,2}"  )
moduleNameRE = re.compile(  R"^[a-zA-Z_][a-zA-Z_0-9]*$"  )
   
def fprintfMessage(stream, format, *args):
    if format[-1:] == '^':
        format = format[:-1]
    else:
        format += '\n'
    if args:
        message = format % args
    else:
        message = format
    stream.write(message)

class Error(Exception):
    pass


class Bundle:
    """Wrap the source, destination and backup paths in one neat little class.
       Used by CheetahWrapper.getBundles().
    """
    def __init__(self, **kw):
        self.__dict__.update(kw)

    def __repr__(self):
        return "<Bundle %r>" % self.__dict__


class MyOptionParser(OptionParser):
    standard_option_list = [] # We use commands for Optik's standard options.

    def error(self, msg):
        """Print our usage+error page."""
        usage(HELP_PAGE2, msg)

    def print_usage(self, file=None):
        """Our usage+error page already has this."""
        pass
    

##################################################
## USAGE FUNCTION & MESSAGES

def usage(usageMessage, errorMessage="", out=sys.stderr):
    """Write help text, an optional error message, and abort the program.
    """
    out.write(WRAPPER_TOP)
    out.write(usageMessage)
    exitStatus = 0
    if errorMessage:
        out.write('\n')
        out.write("*** USAGE ERROR ***: %s\n" % errorMessage)
        exitStatus = 1
    sys.exit(exitStatus)
             

WRAPPER_TOP = """\
         __  ____________  __
         \ \/            \/ /
          \/    *   *     \/    CHEETAH %(Version)s Command-Line Tool
           \      |       / 
            \  ==----==  /      by Tavis Rudd <tavis@damnsimple.com>
             \__________/       and Mike Orr <iron@mso.oz.net>
              
""" % globals()


HELP_PAGE1 = """\
USAGE:
------
  cheetah compile [options] [FILES ...]     : Compile template definitions
  cheetah fill [options] [FILES ...]        : Fill template definitions
  cheetah help                              : Print this help message
  cheetah options                           : Print options help message
  cheetah test [options]                    : Run Cheetah's regression tests
                                            : (same as for unittest)
  cheetah version                           : Print Cheetah version number

You may abbreviate the command to the first letter; e.g., 'h' == 'help'.
If FILES is a single "-", read standard input and write standard output.
Run "cheetah options" for the list of valid options.
"""

HELP_PAGE2 = """\
OPTIONS FOR "compile" AND "fill":
---------------------------------
  --idir DIR, --odir DIR : input/output directories (default: current dir)
  --iext EXT, --oext EXT : input/output filename extensions
    (default for compile: tmpl/py,  fill: tmpl/html)
  -R                 : recurse subdirectories looking for input files
  --debug            : print lots of diagnostic output to standard error
  --env              : put the environment in the searchList
  --flat             : no destination subdirectories
  --nobackup         : don't make backups
  --pickle FILE      : unpickle FILE and put that object in the searchList
  --stdout, -p       : output to standard output (pipe)
  --settings         : a string representing the compiler settings to use
                       e.g. --settings='useNameMapper=False,useFilters=False'
                       This string is eval'd in Python so it should contain
                       valid Python syntax.
  --templateAPIClass : a string representing a subclass of
                       Cheetah.Template:Template to use for compilation

Run "cheetah help" for the main help screen.
"""

##################################################
## CheetahWrapper CLASS

class CheetahWrapper:
    MAKE_BACKUPS = True
    BACKUP_SUFFIX = ".bak"
    _templateClass = None
    _compilerSettings = None    

    def __init__(self):
        self.progName = None
        self.command = None
        self.opts = None
        self.pathArgs = None
        self.sourceFiles = []
        self.searchList = []

    ##################################################
    ## MAIN ROUTINE

    def main(self, argv=None):
        """The main program controller."""

        if argv is None:
            argv = sys.argv

        # Step 1: Determine the command and arguments.
        try:
            self.progName = progName = os.path.basename(argv[0])
            self.command = command = optionDashesRE.sub("", argv[1])
            if command == 'test':
                self.testOpts = argv[2:]
            else:
                self.parseOpts(argv[2:])
        except IndexError:
            usage(HELP_PAGE1, "not enough command-line arguments")

        # Step 2: Call the command
        meths = (self.compile, self.fill, self.help, self.options,
            self.test, self.version)
        for meth in meths:
            methName = meth.__name__
            # Or meth.im_func.func_name
            # Or meth.func_name (Python >= 2.1 only, sometimes works on 2.0)
            methInitial = methName[0]
            if command in (methName, methInitial):
                sys.argv[0] += (" " + methName)
                # @@MO: I don't necessarily agree sys.argv[0] should be 
                # modified.
                meth()
                return
        # If none of the commands matched.
        usage(HELP_PAGE1, "unknown command '%s'" % command)

    def parseOpts(self, args):
        C, D, W = self.chatter, self.debug, self.warn
        self.isCompile = isCompile = self.command[0] == 'c'
        defaultOext = isCompile and ".py" or ".html"
        parser = MyOptionParser()
        pao = parser.add_option
        pao("--idir", action="store", dest="idir", default="")
        pao("--odir", action="store", dest="odir", default="")
        pao("--iext", action="store", dest="iext", default=".tmpl")
        pao("--oext", action="store", dest="oext", default=defaultOext)
        pao("-R", action="store_true", dest="recurse", default=False)
        pao("--stdout", "-p", action="store_true", dest="stdout", default=False)
        pao("--debug", action="store_true", dest="debug", default=False)
        pao("--env", action="store_true", dest="env", default=False)
        pao("--pickle", action="store", dest="pickle", default="")
        pao("--flat", action="store_true", dest="flat", default=False)
        pao("--nobackup", action="store_true", dest="nobackup", default=False)
        pao("--settings", action="store", dest="compilerSettingsString", default=None)
        pao("--templateAPIClass", action="store", dest="templateClassName", default=None)

        self.opts, self.pathArgs = opts, files = parser.parse_args(args)
        D("""\
cheetah compile %s
Options are
%s
Files are %s""", args, pprint.pformat(vars(opts)), files)


        #cleanup trailing path separators
        seps = [sep for sep in [os.sep, os.altsep] if sep]
        for attr in ['idir', 'odir']:
            for sep in seps:
                path = getattr(opts, attr, None)
                if path and path.endswith(sep):
                    path = path[:-len(sep)]
                    setattr(opts, attr, path)
                    break

        self._fixExts()
        if opts.env:
            self.searchList.insert(0, os.environ)
        if opts.pickle:
            f = open(opts.pickle, 'rb')
            unpickled = pickle.load(f)
            f.close()
            self.searchList.insert(0, unpickled)
        opts.verbose = not opts.stdout

    ##################################################
    ## COMMAND METHODS

    def compile(self):
        self._compileOrFill()

    def fill(self):
        from Cheetah.ImportHooks import install
        install()        
        self._compileOrFill()

    def help(self):
        usage(HELP_PAGE1, "", sys.stdout)

    def options(self):
        usage(HELP_PAGE2, "", sys.stdout)

    def test(self):
        # @@MO: Ugly kludge.
        TEST_WRITE_FILENAME = 'cheetah_test_file_creation_ability.tmp'
        try:
            f = open(TEST_WRITE_FILENAME, 'w')
        except:
            sys.exit("""\
Cannot run the tests because you don't have write permission in the current
directory.  The tests need to create temporary files.  Change to a directory
you do have write permission to and re-run the tests.""")
        else:
            f.close()
            os.remove(TEST_WRITE_FILENAME)
        # @@MO: End ugly kludge.
        from Cheetah.Tests import Test
        import Cheetah.Tests.unittest_local_copy as unittest
        del sys.argv[1:] # Prevent unittest from misinterpreting options.
        sys.argv.extend(self.testOpts)
        #unittest.main(testSuite=Test.testSuite)
        #unittest.main(testSuite=Test.testSuite)
        unittest.main(module=Test)
        
    def version(self):
        print Version

    # If you add a command, also add it to the 'meths' variable in main().
    
    ##################################################
    ## LOGGING METHODS

    def chatter(self, format, *args):
        """Print a verbose message to stdout.  But don't if .opts.stdout is
           true or .opts.verbose is false.
        """
        if self.opts.stdout or not self.opts.verbose:
            return
        fprintfMessage(sys.stdout, format, *args)


    def debug(self, format, *args):
        """Print a debugging message to stderr, but don't if .debug is
           false.
        """
        if self.opts.debug:
            fprintfMessage(sys.stderr, format, *args)
    
    def warn(self, format, *args):
        """Always print a warning message to stderr.
        """
        fprintfMessage(sys.stderr, format, *args)

    def error(self, format, *args):
        """Always print a warning message to stderr and exit with an error code.        
        """
        fprintfMessage(sys.stderr, format, *args)
        sys.exit(1)

    ##################################################
    ## HELPER METHODS


    def _fixExts(self):
        assert self.opts.oext, "oext is empty!"
        iext, oext = self.opts.iext, self.opts.oext
        if iext and not iext.startswith("."):
            self.opts.iext = "." + iext
        if oext and not oext.startswith("."):
            self.opts.oext = "." + oext
    


    def _compileOrFill(self):
        C, D, W = self.chatter, self.debug, self.warn
        opts, files = self.opts, self.pathArgs
        if files == ["-"]: 
            self._compileOrFillStdin()
            return
        elif not files and opts.recurse:
            which = opts.idir and "idir" or "current"
            C("Drilling down recursively from %s directory.", which)
            sourceFiles = []
            dir = os.path.join(self.opts.idir, os.curdir)
            os.path.walk(dir, self._expandSourceFilesWalk, sourceFiles)
        elif not files:
            usage(HELP_PAGE1, "Neither files nor -R specified!")
        else:
            sourceFiles = self._expandSourceFiles(files, opts.recurse, True)
        sourceFiles = [os.path.normpath(x) for x in sourceFiles]
        D("All source files found: %s", sourceFiles)
        bundles = self._getBundles(sourceFiles)
        D("All bundles: %s", pprint.pformat(bundles))
        if self.opts.flat:
            self._checkForCollisions(bundles)
        for b in bundles:
            self._compileOrFillBundle(b)

    def _checkForCollisions(self, bundles):
        """Check for multiple source paths writing to the same destination
           path.
        """
        C, D, W = self.chatter, self.debug, self.warn
        isError = False
        dstSources = {}
        for b in bundles:
            if dstSources.has_key(b.dst):
                dstSources[b.dst].append(b.src)
            else:
                dstSources[b.dst] = [b.src]
        keys = dstSources.keys()
        keys.sort()
        for dst in keys:
            sources = dstSources[dst]
            if len(sources) > 1:
                isError = True
                sources.sort()
                fmt = "Collision: multiple source files %s map to one destination file %s"
                W(fmt, sources, dst)
        if isError:
            what = self.isCompile and "Compilation" or "Filling"
            sys.exit("%s aborted due to collisions" % what)
                

    def _expandSourceFilesWalk(self, arg, dir, files):
        """Recursion extension for .expandSourceFiles().
           This method is a callback for os.path.walk().
           'arg' is a list to which successful paths will be appended.
        """
        iext = self.opts.iext
        for f in files:
            path = os.path.join(dir, f)
            if   path.endswith(iext) and os.path.isfile(path):
                arg.append(path)
            elif os.path.islink(path) and os.path.isdir(path):
                os.path.walk(path, self._expandSourceFilesWalk, arg)
            # If is directory, do nothing; 'walk' will eventually get it.


    def _expandSourceFiles(self, files, recurse, addIextIfMissing):
        """Calculate source paths from 'files' by applying the 
           command-line options.
        """
        C, D, W = self.chatter, self.debug, self.warn
        idir = self.opts.idir
        iext = self.opts.iext
        files = [] 
        for f in self.pathArgs:
            oldFilesLen = len(files)
            D("Expanding %s", f)
            path = os.path.join(idir, f)
            pathWithExt = path + iext # May or may not be valid.
            if os.path.isdir(path):
                if recurse:
                    os.path.walk(path, self._expandSourceFilesWalk, files)
                else:
                    raise Error("source file '%s' is a directory" % path)
            elif os.path.isfile(path):
                files.append(path)
            elif (addIextIfMissing and not path.endswith(iext) and 
                  os.path.isfile(pathWithExt)):
                files.append(pathWithExt)
                # Do not recurse directories discovered by iext appending.
            elif os.path.exists(path):
                W("Skipping source file '%s', not a plain file.", path)
            else:
                W("Skipping source file '%s', not found.", path)
            if len(files) > oldFilesLen:
                D("  ... found %s", files[oldFilesLen:])
        return files


    def _getBundles(self, sourceFiles):
        flat = self.opts.flat
        idir = self.opts.idir
        iext = self.opts.iext
        nobackup = self.opts.nobackup
        odir = self.opts.odir
        oext = self.opts.oext
        idirSlash = idir + os.sep
        bundles = []
        for src in sourceFiles:
            # 'base' is the subdirectory plus basename.
            base = src
            if idir and src.startswith(idirSlash):
                base = src[len(idirSlash):]
            if iext and base.endswith(iext):
                base = base[:-len(iext)]
            basename = os.path.basename(base)
            if flat:
                dst = os.path.join(odir, basename + oext)
            else:
                dbn = basename
                if odir and base.startswith(os.sep):
                    odd = odir
                    while odd != '':
                        idx = base.find(odd)
                        if idx == 0:
                            dbn = base[len(odd):]
                            if dbn[0] == '/':
                                dbn = dbn[1:]
                            break
                        odd = os.path.dirname(odd)
                        if odd == '/':
                            break
                    dst = os.path.join(odir, dbn + oext)
                else:
                    dst = os.path.join(odir, base + oext)
            bak = dst + self.BACKUP_SUFFIX
            b = Bundle(src=src, dst=dst, bak=bak, base=base, basename=basename)
            bundles.append(b)
        return bundles


    def _getTemplateClass(self):
        C, D, W = self.chatter, self.debug, self.warn
        modname = None
        if self._templateClass:
            return self._templateClass

        modname = self.opts.templateClassName

        if not modname:
            return Template
        p = modname.rfind('.')
        if ':' not in modname:
            self.error('The value of option --templateAPIClass is invalid\n'
                       'It must be in the form "module:class", '
                       'e.g. "Cheetah.Template:Template"')
            
        modname, classname = modname.split(':')

        C('using --templateAPIClass=%s:%s'%(modname, classname))
        
        if p >= 0:
            mod = getattr(__import__(modname[:p], {}, {}, [modname[p+1:]]), modname[p+1:])
        else:
            mod = __import__(modname, {}, {}, [])

        klass = getattr(mod, classname, None)
        if klass:
            self._templateClass = klass
            return klass
        else:
            self.error('**Template class specified in option --templateAPIClass not found\n'
                       '**Falling back on Cheetah.Template:Template')


    def _getCompilerSettings(self):
        if self._compilerSettings:
            return self._compilerSettings

        def getkws(**kws):
            return kws
        if self.opts.compilerSettingsString:
            try:
                exec 'settings = getkws(%s)'%self.opts.compilerSettingsString
            except:                
                self.error("There's an error in your --settings option."
                          "It must be valid Python syntax.\n"
                          +"    --settings='%s'\n"%self.opts.compilerSettingsString
                          +"  %s: %s"%sys.exc_info()[:2] 
                          )

            validKeys = DEFAULT_COMPILER_SETTINGS.keys()
            if [k for k in settings.keys() if k not in validKeys]:
                self.error(
                    'The --setting "%s" is not a valid compiler setting name.'%k)
            
            self._compilerSettings = settings
            return settings
        else:
            return {}

    def _compileOrFillStdin(self):
        TemplateClass = self._getTemplateClass()
        compilerSettings = self._getCompilerSettings()
        if self.isCompile:
            pysrc = TemplateClass.compile(file=sys.stdin,
                                          compilerSettings=compilerSettings,
                                          returnAClass=False)
            output = pysrc
        else:
            output = str(TemplateClass(file=sys.stdin, compilerSettings=compilerSettings))
        sys.stdout.write(output)

    def _compileOrFillBundle(self, b):
        C, D, W = self.chatter, self.debug, self.warn
        TemplateClass = self._getTemplateClass()
        compilerSettings = self._getCompilerSettings()
        src = b.src
        dst = b.dst
        base = b.base
        basename = b.basename
        dstDir = os.path.dirname(dst)
        what = self.isCompile and "Compiling" or "Filling"
        C("%s %s -> %s^", what, src, dst) # No trailing newline.
        if os.path.exists(dst) and not self.opts.nobackup:
            bak = b.bak
            C(" (backup %s)", bak) # On same line as previous message.
        else:
            bak = None
            C("")
        if self.isCompile:
            if not moduleNameRE.match(basename):
                tup = basename, src
                raise Error("""\
%s: base name %s contains invalid characters.  It must
be named according to the same rules as Python modules.""" % tup)
            pysrc = TemplateClass.compile(file=src, returnAClass=False,
                                          moduleName=basename,
                                          className=basename,
                                          compilerSettings=compilerSettings)
            output = pysrc
        else:
            #output = str(TemplateClass(file=src, searchList=self.searchList))
            tclass = TemplateClass.compile(file=src, compilerSettings=compilerSettings)
            output = str(tclass(searchList=self.searchList))
            
        if bak:
            shutil.copyfile(dst, bak)
        if dstDir and not os.path.exists(dstDir):
            if self.isCompile:
                mkdirsWithPyInitFiles(dstDir)
            else:
                os.makedirs(dstDir)
        if self.opts.stdout:
            sys.stdout.write(output)
        else:
            f = open(dst, 'w')
            f.write(output)
            f.close()
            

##################################################
## if run from the command line
if __name__ == '__main__':  CheetahWrapper().main()

# vim: shiftwidth=4 tabstop=4 expandtab
